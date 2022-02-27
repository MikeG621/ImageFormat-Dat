// Part of the LZMA SDK by Igor Pavlov

using System;

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;

	public partial class Decoder : ICoder, ISetDecoderProperties // ,System.IO.Stream
	{
		readonly LZ.OutWindow _outWindow = new LZ.OutWindow();
		readonly RangeCoder.Decoder _rangeDecoder = new RangeCoder.Decoder();

		readonly BitDecoder[] _isMatchDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
		readonly BitDecoder[] _isRepDecoders = new BitDecoder[Base.kNumStates];
		readonly BitDecoder[] _isRepG0Decoders = new BitDecoder[Base.kNumStates];
		readonly BitDecoder[] _isRepG1Decoders = new BitDecoder[Base.kNumStates];
		readonly BitDecoder[] _isRepG2Decoders = new BitDecoder[Base.kNumStates];
		readonly BitDecoder[] _isRep0LongDecoders = new BitDecoder[Base.kNumStates << Base.kNumPosStatesBitsMax];

		readonly BitTreeDecoder[] _posSlotDecoder = new BitTreeDecoder[Base.kNumLenToPosStates];
		readonly BitDecoder[] _posDecoders = new BitDecoder[Base.kNumFullDistances - Base.kEndPosModelIndex];
		readonly BitTreeDecoder _posAlignDecoder = new BitTreeDecoder(Base.kNumAlignBits);

		readonly LenDecoder _lenDecoder = new LenDecoder();
		readonly LenDecoder _repLenDecoder = new LenDecoder();
		readonly LiteralDecoder _literalDecoder = new LiteralDecoder();

		uint _dictionarySize;
		uint _dictionarySizeCheck;

		uint _posStateMask;

		public Decoder()
		{
			_dictionarySize = 0xFFFFFFFF;
			for (int i = 0; i < Base.kNumLenToPosStates; i++)
				_posSlotDecoder[i] = new BitTreeDecoder(Base.kNumPosSlotBits);
		}

		void setDictionarySize(uint dictionarySize)
		{
			if (_dictionarySize != dictionarySize)
			{
				_dictionarySize = dictionarySize;
				_dictionarySizeCheck = Math.Max(_dictionarySize, 1);
				uint blockSize = Math.Max(_dictionarySizeCheck, (1 << 12));
				_outWindow.Create(blockSize);
			}
		}

		void setLiteralProperties(int lp, int lc)
		{
			if (lp > 8)
				throw new InvalidParamException();
			if (lc > 8)
				throw new InvalidParamException();
			_literalDecoder.Create(lp, lc);
		}

		void setPosBitsProperties(int pb)
		{
			if (pb > Base.kNumPosStatesBitsMax)
				throw new InvalidParamException();
			uint numPosStates = (uint)1 << pb;
			_lenDecoder.Create(numPosStates);
			_repLenDecoder.Create(numPosStates);
			_posStateMask = numPosStates - 1;
		}

		void init(System.IO.Stream inStream, System.IO.Stream outStream)
		{
			_rangeDecoder.Init(inStream);
			_outWindow.Init(outStream, false);

			uint i;
			for (i = 0; i < Base.kNumStates; i++)
			{
				for (uint j = 0; j <= _posStateMask; j++)
				{
					uint index = (i << Base.kNumPosStatesBitsMax) + j;
					_isMatchDecoders[index].Init();
					_isRep0LongDecoders[index].Init();
				}
				_isRepDecoders[i].Init();
				_isRepG0Decoders[i].Init();
				_isRepG1Decoders[i].Init();
				_isRepG2Decoders[i].Init();
			}

			_literalDecoder.Init();
			for (i = 0; i < Base.kNumLenToPosStates; i++)
				_posSlotDecoder[i].Init();
			for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
				_posDecoders[i].Init();

			_lenDecoder.Init();
			_repLenDecoder.Init();
			_posAlignDecoder.Init();
		}

		public void Code(System.IO.Stream inStream, System.IO.Stream outStream,
			long inSize, long outSize, ICodeProgress progress)
		{
			init(inStream, outStream);

			Base.State state = new Base.State();
			state.Init();
			uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;

			ulong nowPos64 = 0;
			ulong outSize64 = (ulong)outSize;
			if (nowPos64 < outSize64)
			{
				if (_isMatchDecoders[state.Index << Base.kNumPosStatesBitsMax].Decode(_rangeDecoder) != 0)
					throw new DataErrorException();
				state.UpdateChar();
				byte b = _literalDecoder.DecodeNormal(_rangeDecoder, 0, 0);
				_outWindow.PutByte(b);
				nowPos64++;
			}
			while (nowPos64 < outSize64)
			{
				// while(nowPos64 < next)
				{
					uint posState = (uint)nowPos64 & _posStateMask;
					if (_isMatchDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(_rangeDecoder) == 0)
					{
						byte b;
						byte prevByte = _outWindow.GetByte(0);
						if (!state.IsCharState())
							b = _literalDecoder.DecodeWithMatchByte(_rangeDecoder,
								(uint)nowPos64, prevByte, _outWindow.GetByte(rep0));
						else
							b = _literalDecoder.DecodeNormal(_rangeDecoder, (uint)nowPos64, prevByte);
						_outWindow.PutByte(b);
						state.UpdateChar();
						nowPos64++;
					}
					else
					{
						uint len;
						if (_isRepDecoders[state.Index].Decode(_rangeDecoder) == 1)
						{
							if (_isRepG0Decoders[state.Index].Decode(_rangeDecoder) == 0)
							{
								if (_isRep0LongDecoders[(state.Index << Base.kNumPosStatesBitsMax) + posState].Decode(_rangeDecoder) == 0)
								{
									state.UpdateShortRep();
									_outWindow.PutByte(_outWindow.GetByte(rep0));
									nowPos64++;
									continue;
								}
							}
							else
							{
								uint distance;
								if (_isRepG1Decoders[state.Index].Decode(_rangeDecoder) == 0)
								{
									distance = rep1;
								}
								else
								{
									if (_isRepG2Decoders[state.Index].Decode(_rangeDecoder) == 0)
										distance = rep2;
									else
									{
										distance = rep3;
										rep3 = rep2;
									}
									rep2 = rep1;
								}
								rep1 = rep0;
								rep0 = distance;
							}
							len = _repLenDecoder.Decode(_rangeDecoder, posState) + Base.kMatchMinLen;
							state.UpdateRep();
						}
						else
						{
							rep3 = rep2;
							rep2 = rep1;
							rep1 = rep0;
							len = Base.kMatchMinLen + _lenDecoder.Decode(_rangeDecoder, posState);
							state.UpdateMatch();
							uint posSlot = _posSlotDecoder[Base.GetLenToPosState(len)].Decode(_rangeDecoder);
							if (posSlot >= Base.kStartPosModelIndex)
							{
								int numDirectBits = (int)((posSlot >> 1) - 1);
								rep0 = ((2 | (posSlot & 1)) << numDirectBits);
								if (posSlot < Base.kEndPosModelIndex)
									rep0 += BitTreeDecoder.ReverseDecode(_posDecoders,
											rep0 - posSlot - 1, _rangeDecoder, numDirectBits);
								else
								{
									rep0 += (_rangeDecoder.DecodeDirectBits(
										numDirectBits - Base.kNumAlignBits) << Base.kNumAlignBits);
									rep0 += _posAlignDecoder.ReverseDecode(_rangeDecoder);
								}
							}
							else
								rep0 = posSlot;
						}
						if (rep0 >= _outWindow.TrainSize + nowPos64 || rep0 >= _dictionarySizeCheck)
						{
							if (rep0 == 0xFFFFFFFF)
								break;
							throw new DataErrorException();
						}
						_outWindow.CopyBlock(rep0, len);
						nowPos64 += len;
					}
				}
			}
			_outWindow.Flush();
			_outWindow.ReleaseStream();
			_rangeDecoder.ReleaseStream();
		}

		public void SetDecoderProperties(byte[] properties)
		{
			if (properties.Length < 5)
				throw new InvalidParamException();
			int lc = properties[0] % 9;
			int remainder = properties[0] / 9;
			int lp = remainder % 5;
			int pb = remainder / 5;
			if (pb > Base.kNumPosStatesBitsMax)
				throw new InvalidParamException();
			uint dictionarySize = 0;
			for (int i = 0; i < 4; i++)
				dictionarySize += ((uint)(properties[1 + i])) << (i * 8);
			setDictionarySize(dictionarySize);
			setLiteralProperties(lp, lc);
			setPosBitsProperties(pb);
		}
	}
}
