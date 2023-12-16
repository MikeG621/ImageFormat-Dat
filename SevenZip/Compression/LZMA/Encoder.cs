// Part of the LZMA SDK by Igor Pavlov

using System;

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;

	public partial class Encoder : ICoder, ISetCoderProperties, IWriteCoderProperties
	{
		enum EMatchFinderType
		{
			BT2,
			BT4,
		};

#pragma warning disable IDE1006 // Naming Styles
		const uint kIfinityPrice = 0xFFFFFFF;
		const int kDefaultDictionaryLogSize = 22;
		const uint kNumFastBytesDefault = 0x20;
		const uint kNumOpts = 1 << 12;
		const int kPropSize = 5;
		static readonly string[] kMatchFinderIDs =
		{
			"BT2",
			"BT4",
		};
#pragma warning restore IDE1006 // Naming Styles

		static readonly byte[] _fastPos = new byte[1 << 11];

		static Encoder()
		{
			const byte kFastSlots = 22;
			int c = 2;
			_fastPos[0] = 0;
			_fastPos[1] = 1;
			for (byte slotFast = 2; slotFast < kFastSlots; slotFast++)
			{
				uint k = ((uint)1 << ((slotFast >> 1) - 1));
				for (uint j = 0; j < k; j++, c++)
					_fastPos[c] = slotFast;
			}
		}

		static uint getPosSlot(uint pos)
		{
			if (pos < (1 << 11))
				return _fastPos[pos];
			if (pos < (1 << 21))
				return (uint)(_fastPos[pos >> 10] + 20);
			return (uint)(_fastPos[pos >> 20] + 40);
		}

		static uint getPosSlot2(uint pos)
		{
			if (pos < (1 << 17))
				return (uint)(_fastPos[pos >> 6] + 12);
			if (pos < (1 << 27))
				return (uint)(_fastPos[pos >> 16] + 32);
			return (uint)(_fastPos[pos >> 26] + 52);
		}

		Base.State _state = new Base.State();
		byte _previousByte;
		readonly uint[] _repDistances = new uint[Base.kNumRepDistances];

		void baseInit()
		{
			_state.Init();
			_previousByte = 0;
			for (uint i = 0; i < Base.kNumRepDistances; i++)
				_repDistances[i] = 0;
		}

		readonly Optimal[] _optimum = new Optimal[kNumOpts];
		LZ.IMatchFinder _matchFinder = null;
		readonly RangeCoder.Encoder _rangeEncoder = new RangeCoder.Encoder();
		readonly BitEncoder[] _isMatch = new BitEncoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
		readonly BitEncoder[] _isRep = new BitEncoder[Base.kNumStates];
		readonly BitEncoder[] _isRepG0 = new BitEncoder[Base.kNumStates];
		readonly BitEncoder[] _isRepG1 = new BitEncoder[Base.kNumStates];
		readonly BitEncoder[] _isRepG2 = new BitEncoder[Base.kNumStates];
		readonly BitEncoder[] _isRep0Long = new BitEncoder[Base.kNumStates << Base.kNumPosStatesBitsMax];
		readonly BitTreeEncoder[] _posSlotEncoder = new BitTreeEncoder[Base.kNumLenToPosStates];
		readonly BitEncoder[] _posEncoders = new BitEncoder[Base.kNumFullDistances - Base.kEndPosModelIndex];
		readonly BitTreeEncoder _posAlignEncoder = new BitTreeEncoder(Base.kNumAlignBits);
		readonly LenPriceTableEncoder _lenEncoder = new LenPriceTableEncoder();
		readonly LenPriceTableEncoder _repMatchLenEncoder = new LenPriceTableEncoder();
		readonly LiteralEncoder _literalEncoder = new LiteralEncoder();
		readonly uint[] _matchDistances = new uint[Base.kMatchMaxLen * 2 + 2];

		uint _numFastBytes = kNumFastBytesDefault;
		uint _longestMatchLength;
		uint _numDistancePairs;

		uint _additionalOffset;

		uint _optimumEndIndex;
		uint _optimumCurrentIndex;

		bool _longestMatchWasFound;
		readonly uint[] _posSlotPrices = new uint[1 << (Base.kNumPosSlotBits + Base.kNumLenToPosStatesBits)];
		readonly uint[] _distancesPrices = new uint[Base.kNumFullDistances << Base.kNumLenToPosStatesBits];
		readonly uint[] _alignPrices = new uint[Base.kAlignTableSize];
		uint _alignPriceCount;

		uint _distTableSize = (kDefaultDictionaryLogSize * 2);

		int _posStateBits = 2;
		uint _posStateMask = (4 - 1);
		int _numLiteralPosStateBits = 0;
		int _numLiteralContextBits = 3;

		uint _dictionarySize = (1 << kDefaultDictionaryLogSize);
		uint _dictionarySizePrev = 0xFFFFFFFF;
		uint _numFastBytesPrev = 0xFFFFFFFF;

		long _nowPos64;
		bool _finished;
		System.IO.Stream _inStream;

		EMatchFinderType _matchFinderType = EMatchFinderType.BT4;
		bool _writeEndMark = false;
		
		bool _needReleaseMFStream;

		void create()
		{
			if (_matchFinder == null)
			{
				LZ.BinTree bt = new LZ.BinTree();
				int numHashBytes = 4;
				if (_matchFinderType == EMatchFinderType.BT2)
					numHashBytes = 2;
				bt.SetType(numHashBytes);
				_matchFinder = bt;
			}
			_literalEncoder.Create(_numLiteralPosStateBits, _numLiteralContextBits);

			if (_dictionarySize == _dictionarySizePrev && _numFastBytesPrev == _numFastBytes)
				return;
			_matchFinder.Create(_dictionarySize, kNumOpts, _numFastBytes, Base.kMatchMaxLen + 1);
			_dictionarySizePrev = _dictionarySize;
			_numFastBytesPrev = _numFastBytes;
		}

		public Encoder()
		{
			for (int i = 0; i < kNumOpts; i++)
				_optimum[i] = new Optimal();
			for (int i = 0; i < Base.kNumLenToPosStates; i++)
				_posSlotEncoder[i] = new BitTreeEncoder(Base.kNumPosSlotBits);
		}

		void setWriteEndMarkerMode(bool writeEndMarker)
		{
			_writeEndMark = writeEndMarker;
		}

		void init()
		{
			baseInit();
			_rangeEncoder.Init();

			uint i;
			for (i = 0; i < Base.kNumStates; i++)
			{
				for (uint j = 0; j <= _posStateMask; j++)
				{
					uint complexState = (i << Base.kNumPosStatesBitsMax) + j;
					_isMatch[complexState].Init();
					_isRep0Long[complexState].Init();
				}
				_isRep[i].Init();
				_isRepG0[i].Init();
				_isRepG1[i].Init();
				_isRepG2[i].Init();
			}
			_literalEncoder.Init();
			for (i = 0; i < Base.kNumLenToPosStates; i++)
				_posSlotEncoder[i].Init();
			for (i = 0; i < Base.kNumFullDistances - Base.kEndPosModelIndex; i++)
				_posEncoders[i].Init();

			_lenEncoder.Init((uint)1 << _posStateBits);
			_repMatchLenEncoder.Init((uint)1 << _posStateBits);

			_posAlignEncoder.Init();

			_longestMatchWasFound = false;
			_optimumEndIndex = 0;
			_optimumCurrentIndex = 0;
			_additionalOffset = 0;
		}

		void readMatchDistances(out uint lenRes, out uint numDistancePairs)
		{
			lenRes = 0;
			numDistancePairs = _matchFinder.GetMatches(_matchDistances);
			if (numDistancePairs > 0)
			{
				lenRes = _matchDistances[numDistancePairs - 2];
				if (lenRes == _numFastBytes)
					lenRes += _matchFinder.GetMatchLen((int)lenRes - 1, _matchDistances[numDistancePairs - 1],
						Base.kMatchMaxLen - lenRes);
			}
			_additionalOffset++;
		}


		void movePos(uint num)
		{
			if (num > 0)
			{
				_matchFinder.Skip(num);
				_additionalOffset += num;
			}
		}

		uint getRepLen1Price(Base.State state, uint posState)
		{
			return _isRepG0[state.Index].GetPrice0() +
					_isRep0Long[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0();
		}

		uint getPureRepPrice(uint repIndex, Base.State state, uint posState)
		{
			uint price;
			if (repIndex == 0)
			{
				price = _isRepG0[state.Index].GetPrice0();
				price += _isRep0Long[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
			}
			else
			{
				price = _isRepG0[state.Index].GetPrice1();
				if (repIndex == 1)
					price += _isRepG1[state.Index].GetPrice0();
				else
				{
					price += _isRepG1[state.Index].GetPrice1();
					price += _isRepG2[state.Index].GetPrice(repIndex - 2);
				}
			}
			return price;
		}

		uint getRepPrice(uint repIndex, uint len, Base.State state, uint posState)
		{
			uint price = _repMatchLenEncoder.GetPrice(len - Base.kMatchMinLen, posState);
			return price + getPureRepPrice(repIndex, state, posState);
		}

		uint getPosLenPrice(uint pos, uint len, uint posState)
		{
			uint price;
			uint lenToPosState = Base.GetLenToPosState(len);
			if (pos < Base.kNumFullDistances)
				price = _distancesPrices[(lenToPosState * Base.kNumFullDistances) + pos];
			else
				price = _posSlotPrices[(lenToPosState << Base.kNumPosSlotBits) + getPosSlot2(pos)] +
					_alignPrices[pos & Base.kAlignMask];
			return price + _lenEncoder.GetPrice(len - Base.kMatchMinLen, posState);
		}

		uint backward(out uint backRes, uint cur)
		{
			_optimumEndIndex = cur;
			uint posMem = _optimum[cur].PosPrev;
			uint backMem = _optimum[cur].BackPrev;
			do
			{
				if (_optimum[cur].Prev1IsChar)
				{
					_optimum[posMem].MakeAsChar();
					_optimum[posMem].PosPrev = posMem - 1;
					if (_optimum[cur].Prev2)
					{
						_optimum[posMem - 1].Prev1IsChar = false;
						_optimum[posMem - 1].PosPrev = _optimum[cur].PosPrev2;
						_optimum[posMem - 1].BackPrev = _optimum[cur].BackPrev2;
					}
				}
				uint posPrev = posMem;
				uint backCur = backMem;

				backMem = _optimum[posPrev].BackPrev;
				posMem = _optimum[posPrev].PosPrev;

				_optimum[posPrev].BackPrev = backCur;
				_optimum[posPrev].PosPrev = cur;
				cur = posPrev;
			}
			while (cur > 0);
			backRes = _optimum[0].BackPrev;
			_optimumCurrentIndex = _optimum[0].PosPrev;
			return _optimumCurrentIndex;
		}

		readonly uint[] _reps = new uint[Base.kNumRepDistances];
		readonly uint[] _repLens = new uint[Base.kNumRepDistances];

		uint getOptimum(uint position, out uint backRes)
		{
			if (_optimumEndIndex != _optimumCurrentIndex)
			{
				uint lenRes = _optimum[_optimumCurrentIndex].PosPrev - _optimumCurrentIndex;
				backRes = _optimum[_optimumCurrentIndex].BackPrev;
				_optimumCurrentIndex = _optimum[_optimumCurrentIndex].PosPrev;
				return lenRes;
			}
			_optimumCurrentIndex = _optimumEndIndex = 0;

			uint lenMain, numDistancePairs;
			if (!_longestMatchWasFound)
			{
				readMatchDistances(out lenMain, out numDistancePairs);
			}
			else
			{
				lenMain = _longestMatchLength;
				numDistancePairs = _numDistancePairs;
				_longestMatchWasFound = false;
			}

			uint numAvailableBytes = _matchFinder.GetNumAvailableBytes() + 1;
			if (numAvailableBytes < 2)
			{
				backRes = 0xFFFFFFFF;
				return 1;
			}

			uint repMaxIndex = 0;
			uint i;			
			for (i = 0; i < Base.kNumRepDistances; i++)
			{
				_reps[i] = _repDistances[i];
				_repLens[i] = _matchFinder.GetMatchLen(0 - 1, _reps[i], Base.kMatchMaxLen);
				if (_repLens[i] > _repLens[repMaxIndex])
					repMaxIndex = i;
			}
			if (_repLens[repMaxIndex] >= _numFastBytes)
			{
				backRes = repMaxIndex;
				uint lenRes = _repLens[repMaxIndex];
				movePos(lenRes - 1);
				return lenRes;
			}

			if (lenMain >= _numFastBytes)
			{
				backRes = _matchDistances[numDistancePairs - 1] + Base.kNumRepDistances;
				movePos(lenMain - 1);
				return lenMain;
			}

			byte currentByte = _matchFinder.GetIndexByte(0 - 1);
			byte matchByte = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - 1));

			if (lenMain < 2 && currentByte != matchByte && _repLens[repMaxIndex] < 2)
			{
				backRes = (uint)0xFFFFFFFF;
				return 1;
			}

			_optimum[0].State = _state;

			uint posState = (position & _posStateMask);

			_optimum[1].Price = _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0() +
					_literalEncoder.GetSubCoder(position, _previousByte).GetPrice(!_state.IsCharState(), matchByte, currentByte);
			_optimum[1].MakeAsChar();

			uint matchPrice = _isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
			uint repMatchPrice = matchPrice + _isRep[_state.Index].GetPrice1();

			if (matchByte == currentByte)
			{
				uint shortRepPrice = repMatchPrice + getRepLen1Price(_state, posState);
				if (shortRepPrice < _optimum[1].Price)
				{
					_optimum[1].Price = shortRepPrice;
					_optimum[1].MakeAsShortRep();
				}
			}

			uint lenEnd = ((lenMain >= _repLens[repMaxIndex]) ? lenMain : _repLens[repMaxIndex]);

			if(lenEnd < 2)
			{
				backRes = _optimum[1].BackPrev;
				return 1;
			}
			
			_optimum[1].PosPrev = 0;

			_optimum[0].Backs0 = _reps[0];
			_optimum[0].Backs1 = _reps[1];
			_optimum[0].Backs2 = _reps[2];
			_optimum[0].Backs3 = _reps[3];

			uint len = lenEnd;
			do
				_optimum[len--].Price = kIfinityPrice;
			while (len >= 2);

			for (i = 0; i < Base.kNumRepDistances; i++)
			{
				uint repLen = _repLens[i];
				if (repLen < 2)
					continue;
				uint price = repMatchPrice + getPureRepPrice(i, _state, posState);
				do
				{
					uint curAndLenPrice = price + _repMatchLenEncoder.GetPrice(repLen - 2, posState);
					Optimal optimum = _optimum[repLen];
					if (curAndLenPrice < optimum.Price)
					{
						optimum.Price = curAndLenPrice;
						optimum.PosPrev = 0;
						optimum.BackPrev = i;
						optimum.Prev1IsChar = false;
					}
				}
				while (--repLen >= 2);
			}

			uint normalMatchPrice = matchPrice + _isRep[_state.Index].GetPrice0();
			
			len = ((_repLens[0] >= 2) ? _repLens[0] + 1 : 2);
			if (len <= lenMain)
			{
				uint offs = 0;
				while (len > _matchDistances[offs])
					offs += 2;
				for (; ; len++)
				{
					uint distance = _matchDistances[offs + 1];
					uint curAndLenPrice = normalMatchPrice + getPosLenPrice(distance, len, posState);
					Optimal optimum = _optimum[len];
					if (curAndLenPrice < optimum.Price)
					{
						optimum.Price = curAndLenPrice;
						optimum.PosPrev = 0;
						optimum.BackPrev = distance + Base.kNumRepDistances;
						optimum.Prev1IsChar = false;
					}
					if (len == _matchDistances[offs])
					{
						offs += 2;
						if (offs == numDistancePairs)
							break;
					}
				}
			}

			uint cur = 0;

			while (true)
			{
				cur++;
				if (cur == lenEnd)
					return backward(out backRes, cur);
				readMatchDistances(out uint newLen, out numDistancePairs);
				if (newLen >= _numFastBytes)
				{
					_numDistancePairs = numDistancePairs;
					_longestMatchLength = newLen;
					_longestMatchWasFound = true;
					return backward(out backRes, cur);
				}
				position++;
				uint posPrev = _optimum[cur].PosPrev;
				Base.State state;
				if (_optimum[cur].Prev1IsChar)
				{
					posPrev--;
					if (_optimum[cur].Prev2)
					{
						state = _optimum[_optimum[cur].PosPrev2].State;
						if (_optimum[cur].BackPrev2 < Base.kNumRepDistances)
							state.UpdateRep();
						else
							state.UpdateMatch();
					}
					else
						state = _optimum[posPrev].State;
					state.UpdateChar();
				}
				else
					state = _optimum[posPrev].State;
				if (posPrev == cur - 1)
				{
					if (_optimum[cur].IsShortRep())
						state.UpdateShortRep();
					else
						state.UpdateChar();
				}
				else
				{
					uint pos;
					if (_optimum[cur].Prev1IsChar && _optimum[cur].Prev2)
					{
						posPrev = _optimum[cur].PosPrev2;
						pos = _optimum[cur].BackPrev2;
						state.UpdateRep();
					}
					else
					{
						pos = _optimum[cur].BackPrev;
						if (pos < Base.kNumRepDistances)
							state.UpdateRep();
						else
							state.UpdateMatch();
					}
					Optimal opt = _optimum[posPrev];
					if (pos < Base.kNumRepDistances)
					{
						if (pos == 0)
						{
							_reps[0] = opt.Backs0;
							_reps[1] = opt.Backs1;
							_reps[2] = opt.Backs2;
							_reps[3] = opt.Backs3;
						}
						else if (pos == 1)
						{
							_reps[0] = opt.Backs1;
							_reps[1] = opt.Backs0;
							_reps[2] = opt.Backs2;
							_reps[3] = opt.Backs3;
						}
						else if (pos == 2)
						{
							_reps[0] = opt.Backs2;
							_reps[1] = opt.Backs0;
							_reps[2] = opt.Backs1;
							_reps[3] = opt.Backs3;
						}
						else
						{
							_reps[0] = opt.Backs3;
							_reps[1] = opt.Backs0;
							_reps[2] = opt.Backs1;
							_reps[3] = opt.Backs2;
						}
					}
					else
					{
						_reps[0] = (pos - Base.kNumRepDistances);
						_reps[1] = opt.Backs0;
						_reps[2] = opt.Backs1;
						_reps[3] = opt.Backs2;
					}
				}
				_optimum[cur].State = state;
				_optimum[cur].Backs0 = _reps[0];
				_optimum[cur].Backs1 = _reps[1];
				_optimum[cur].Backs2 = _reps[2];
				_optimum[cur].Backs3 = _reps[3];
				uint curPrice = _optimum[cur].Price;

				currentByte = _matchFinder.GetIndexByte(0 - 1);
				matchByte = _matchFinder.GetIndexByte((int)(0 - _reps[0] - 1 - 1));

				posState = (position & _posStateMask);

				uint curAnd1Price = curPrice +
					_isMatch[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice0() +
					_literalEncoder.GetSubCoder(position, _matchFinder.GetIndexByte(0 - 2)).
					GetPrice(!state.IsCharState(), matchByte, currentByte);

				Optimal nextOptimum = _optimum[cur + 1];

				bool nextIsChar = false;
				if (curAnd1Price < nextOptimum.Price)
				{
					nextOptimum.Price = curAnd1Price;
					nextOptimum.PosPrev = cur;
					nextOptimum.MakeAsChar();
					nextIsChar = true;
				}

				matchPrice = curPrice + _isMatch[(state.Index << Base.kNumPosStatesBitsMax) + posState].GetPrice1();
				repMatchPrice = matchPrice + _isRep[state.Index].GetPrice1();

				if (matchByte == currentByte &&
					!(nextOptimum.PosPrev < cur && nextOptimum.BackPrev == 0))
				{
					uint shortRepPrice = repMatchPrice + getRepLen1Price(state, posState);
					if (shortRepPrice <= nextOptimum.Price)
					{
						nextOptimum.Price = shortRepPrice;
						nextOptimum.PosPrev = cur;
						nextOptimum.MakeAsShortRep();
						nextIsChar = true;
					}
				}

				uint numAvailableBytesFull = _matchFinder.GetNumAvailableBytes() + 1;
				numAvailableBytesFull = Math.Min(kNumOpts - 1 - cur, numAvailableBytesFull);
				numAvailableBytes = numAvailableBytesFull;

				if (numAvailableBytes < 2)
					continue;
				if (numAvailableBytes > _numFastBytes)
					numAvailableBytes = _numFastBytes;
				if (!nextIsChar && matchByte != currentByte)
				{
					// try Literal + rep0
					uint t = Math.Min(numAvailableBytesFull - 1, _numFastBytes);
					uint lenTest2 = _matchFinder.GetMatchLen(0, _reps[0], t);
					if (lenTest2 >= 2)
					{
						Base.State state2 = state;
						state2.UpdateChar();
						uint posStateNext = (position + 1) & _posStateMask;
						uint nextRepMatchPrice = curAnd1Price +
							_isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1() +
							_isRep[state2.Index].GetPrice1();
						{
							uint offset = cur + 1 + lenTest2;
							while (lenEnd < offset)
								_optimum[++lenEnd].Price = kIfinityPrice;
							uint curAndLenPrice = nextRepMatchPrice + getRepPrice(
								0, lenTest2, state2, posStateNext);
							Optimal optimum = _optimum[offset];
							if (curAndLenPrice < optimum.Price)
							{
								optimum.Price = curAndLenPrice;
								optimum.PosPrev = cur + 1;
								optimum.BackPrev = 0;
								optimum.Prev1IsChar = true;
								optimum.Prev2 = false;
							}
						}
					}
				}

				uint startLen = 2; // speed optimization 

				for (uint repIndex = 0; repIndex < Base.kNumRepDistances; repIndex++)
				{
					uint lenTest = _matchFinder.GetMatchLen(0 - 1, _reps[repIndex], numAvailableBytes);
					if (lenTest < 2)
						continue;
					uint lenTestTemp = lenTest;
					do
					{
						while (lenEnd < cur + lenTest)
							_optimum[++lenEnd].Price = kIfinityPrice;
						uint curAndLenPrice = repMatchPrice + getRepPrice(repIndex, lenTest, state, posState);
						Optimal optimum = _optimum[cur + lenTest];
						if (curAndLenPrice < optimum.Price)
						{
							optimum.Price = curAndLenPrice;
							optimum.PosPrev = cur;
							optimum.BackPrev = repIndex;
							optimum.Prev1IsChar = false;
						}
					}
					while(--lenTest >= 2);
					lenTest = lenTestTemp;

					if (repIndex == 0)
						startLen = lenTest + 1;

					// if (_maxMode)
					if (lenTest < numAvailableBytesFull)
					{
						uint t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
						uint lenTest2 = _matchFinder.GetMatchLen((int)lenTest, _reps[repIndex], t);
						if (lenTest2 >= 2)
						{
							Base.State state2 = state;
							state2.UpdateRep();
							uint posStateNext = (position + lenTest) & _posStateMask;
							uint curAndLenCharPrice = 
									repMatchPrice + getRepPrice(repIndex, lenTest, state, posState) + 
									_isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice0() +
									_literalEncoder.GetSubCoder(position + lenTest, 
									_matchFinder.GetIndexByte((int)lenTest - 1 - 1)).GetPrice(true,
									_matchFinder.GetIndexByte((int)((int)lenTest - 1 - (int)(_reps[repIndex] + 1))), 
									_matchFinder.GetIndexByte((int)lenTest - 1));
							state2.UpdateChar();
							posStateNext = (position + lenTest + 1) & _posStateMask;
							uint nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1();
							uint nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();
							
							// for(; lenTest2 >= 2; lenTest2--)
							{
								uint offset = lenTest + 1 + lenTest2;
								while(lenEnd < cur + offset)
									_optimum[++lenEnd].Price = kIfinityPrice;
								uint curAndLenPrice = nextRepMatchPrice + getRepPrice(0, lenTest2, state2, posStateNext);
								Optimal optimum = _optimum[cur + offset];
								if (curAndLenPrice < optimum.Price) 
								{
									optimum.Price = curAndLenPrice;
									optimum.PosPrev = cur + lenTest + 1;
									optimum.BackPrev = 0;
									optimum.Prev1IsChar = true;
									optimum.Prev2 = true;
									optimum.PosPrev2 = cur;
									optimum.BackPrev2 = repIndex;
								}
							}
						}
					}
				}

				if (newLen > numAvailableBytes)
				{
					newLen = numAvailableBytes;
					for (numDistancePairs = 0; newLen > _matchDistances[numDistancePairs]; numDistancePairs += 2) ;
					_matchDistances[numDistancePairs] = newLen;
					numDistancePairs += 2;
				}
				if (newLen >= startLen)
				{
					normalMatchPrice = matchPrice + _isRep[state.Index].GetPrice0();
					while (lenEnd < cur + newLen)
						_optimum[++lenEnd].Price = kIfinityPrice;

					uint offs = 0;
					while (startLen > _matchDistances[offs])
						offs += 2;

					for (uint lenTest = startLen; ; lenTest++)
					{
						uint curBack = _matchDistances[offs + 1];
						uint curAndLenPrice = normalMatchPrice + getPosLenPrice(curBack, lenTest, posState);
						Optimal optimum = _optimum[cur + lenTest];
						if (curAndLenPrice < optimum.Price)
						{
							optimum.Price = curAndLenPrice;
							optimum.PosPrev = cur;
							optimum.BackPrev = curBack + Base.kNumRepDistances;
							optimum.Prev1IsChar = false;
						}

						if (lenTest == _matchDistances[offs])
						{
							if (lenTest < numAvailableBytesFull)
							{
								uint t = Math.Min(numAvailableBytesFull - 1 - lenTest, _numFastBytes);
								uint lenTest2 = _matchFinder.GetMatchLen((int)lenTest, curBack, t);
								if (lenTest2 >= 2)
								{
									Base.State state2 = state;
									state2.UpdateMatch();
									uint posStateNext = (position + lenTest) & _posStateMask;
									uint curAndLenCharPrice = curAndLenPrice +
										_isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice0() +
										_literalEncoder.GetSubCoder(position + lenTest,
										_matchFinder.GetIndexByte((int)lenTest - 1 - 1)).
										GetPrice(true,
										_matchFinder.GetIndexByte((int)lenTest - (int)(curBack + 1) - 1),
										_matchFinder.GetIndexByte((int)lenTest - 1));
									state2.UpdateChar();
									posStateNext = (position + lenTest + 1) & _posStateMask;
									uint nextMatchPrice = curAndLenCharPrice + _isMatch[(state2.Index << Base.kNumPosStatesBitsMax) + posStateNext].GetPrice1();
									uint nextRepMatchPrice = nextMatchPrice + _isRep[state2.Index].GetPrice1();

									uint offset = lenTest + 1 + lenTest2;
									while (lenEnd < cur + offset)
										_optimum[++lenEnd].Price = kIfinityPrice;
									curAndLenPrice = nextRepMatchPrice + getRepPrice(0, lenTest2, state2, posStateNext);
									optimum = _optimum[cur + offset];
									if (curAndLenPrice < optimum.Price)
									{
										optimum.Price = curAndLenPrice;
										optimum.PosPrev = cur + lenTest + 1;
										optimum.BackPrev = 0;
										optimum.Prev1IsChar = true;
										optimum.Prev2 = true;
										optimum.PosPrev2 = cur;
										optimum.BackPrev2 = curBack + Base.kNumRepDistances;
									}
								}
							}
							offs += 2;
							if (offs == numDistancePairs)
								break;
						}
					}
				}
			}
		}

		void writeEndMarker(uint posState)
		{
			if (!_writeEndMark)
				return;

			_isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 1);
			_isRep[_state.Index].Encode(_rangeEncoder, 0);
			_state.UpdateMatch();
			uint len = Base.kMatchMinLen;
			_lenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
			uint posSlot = (1 << Base.kNumPosSlotBits) - 1;
			uint lenToPosState = Base.GetLenToPosState(len);
			_posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);
			int footerBits = 30;
			uint posReduced = (((uint)1) << footerBits) - 1;
			_rangeEncoder.EncodeDirectBits(posReduced >> Base.kNumAlignBits, footerBits - Base.kNumAlignBits);
			_posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.kAlignMask);
		}

		void flush(uint nowPos)
		{
			releaseMFStream();
			writeEndMarker(nowPos & _posStateMask);
			_rangeEncoder.FlushData();
			_rangeEncoder.FlushStream();
		}

		public void CodeOneBlock(out long inSize, out long outSize, out bool finished)
		{
			inSize = 0;
			outSize = 0;
			finished = true;

			if (_inStream != null)
			{
				_matchFinder.SetStream(_inStream);
				_matchFinder.Init();
				_needReleaseMFStream = true;
				_inStream = null;
			}

			if (_finished)
				return;
			_finished = true;


			long progressPosValuePrev = _nowPos64;
			if (_nowPos64 == 0)
			{
				if (_matchFinder.GetNumAvailableBytes() == 0)
				{
					flush((uint)_nowPos64);
					return;
				}
				// it's not used
				readMatchDistances(out _, out _);
				uint posState = (uint)(_nowPos64) & _posStateMask;
				_isMatch[(_state.Index << Base.kNumPosStatesBitsMax) + posState].Encode(_rangeEncoder, 0);
				_state.UpdateChar();
				byte curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
				_literalEncoder.GetSubCoder((uint)(_nowPos64), _previousByte).Encode(_rangeEncoder, curByte);
				_previousByte = curByte;
				_additionalOffset--;
				_nowPos64++;
			}
			if (_matchFinder.GetNumAvailableBytes() == 0)
			{
				flush((uint)_nowPos64);
				return;
			}
			while (true)
			{
				uint len = getOptimum((uint)_nowPos64, out uint pos);

				uint posState = ((uint)_nowPos64) & _posStateMask;
				uint complexState = (_state.Index << Base.kNumPosStatesBitsMax) + posState;
				if (len == 1 && pos == 0xFFFFFFFF)
				{
					_isMatch[complexState].Encode(_rangeEncoder, 0);
					byte curByte = _matchFinder.GetIndexByte((int)(0 - _additionalOffset));
					LiteralEncoder.Encoder2 subCoder = _literalEncoder.GetSubCoder((uint)_nowPos64, _previousByte);
					if (!_state.IsCharState())
					{
						byte matchByte = _matchFinder.GetIndexByte((int)(0 - _repDistances[0] - 1 - _additionalOffset));
						subCoder.EncodeMatched(_rangeEncoder, matchByte, curByte);
					}
					else
						subCoder.Encode(_rangeEncoder, curByte);
					_previousByte = curByte;
					_state.UpdateChar();
				}
				else
				{
					_isMatch[complexState].Encode(_rangeEncoder, 1);
					if (pos < Base.kNumRepDistances)
					{
						_isRep[_state.Index].Encode(_rangeEncoder, 1);
						if (pos == 0)
						{
							_isRepG0[_state.Index].Encode(_rangeEncoder, 0);
							if (len == 1)
								_isRep0Long[complexState].Encode(_rangeEncoder, 0);
							else
								_isRep0Long[complexState].Encode(_rangeEncoder, 1);
						}
						else
						{
							_isRepG0[_state.Index].Encode(_rangeEncoder, 1);
							if (pos == 1)
								_isRepG1[_state.Index].Encode(_rangeEncoder, 0);
							else
							{
								_isRepG1[_state.Index].Encode(_rangeEncoder, 1);
								_isRepG2[_state.Index].Encode(_rangeEncoder, pos - 2);
							}
						}
						if (len == 1)
							_state.UpdateShortRep();
						else
						{
							_repMatchLenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
							_state.UpdateRep();
						}
						uint distance = _repDistances[pos];
						if (pos != 0)
						{
							for (uint i = pos; i >= 1; i--)
								_repDistances[i] = _repDistances[i - 1];
							_repDistances[0] = distance;
						}
					}
					else
					{
						_isRep[_state.Index].Encode(_rangeEncoder, 0);
						_state.UpdateMatch();
						_lenEncoder.Encode(_rangeEncoder, len - Base.kMatchMinLen, posState);
						pos -= Base.kNumRepDistances;
						uint posSlot = getPosSlot(pos);
						uint lenToPosState = Base.GetLenToPosState(len);
						_posSlotEncoder[lenToPosState].Encode(_rangeEncoder, posSlot);

						if (posSlot >= Base.kStartPosModelIndex)
						{
							int footerBits = (int)((posSlot >> 1) - 1);
							uint baseVal = ((2 | (posSlot & 1)) << footerBits);
							uint posReduced = pos - baseVal;

							if (posSlot < Base.kEndPosModelIndex)
								RangeCoder.BitTreeEncoder.ReverseEncode(_posEncoders,
										baseVal - posSlot - 1, _rangeEncoder, footerBits, posReduced);
							else
							{
								_rangeEncoder.EncodeDirectBits(posReduced >> Base.kNumAlignBits, footerBits - Base.kNumAlignBits);
								_posAlignEncoder.ReverseEncode(_rangeEncoder, posReduced & Base.kAlignMask);
								_alignPriceCount++;
							}
						}
						uint distance = pos;
						for (uint i = Base.kNumRepDistances - 1; i >= 1; i--)
							_repDistances[i] = _repDistances[i - 1];
						_repDistances[0] = distance;
						_matchPriceCount++;
					}
					_previousByte = _matchFinder.GetIndexByte((int)(len - 1 - _additionalOffset));
				}
				_additionalOffset -= len;
				_nowPos64 += len;
				if (_additionalOffset == 0)
				{
					// if (!_fastMode)
					if (_matchPriceCount >= (1 << 7))
						fillDistancesPrices();
					if (_alignPriceCount >= Base.kAlignTableSize)
						fillAlignPrices();
					inSize = _nowPos64;
					outSize = _rangeEncoder.GetProcessedSizeAdd();
					if (_matchFinder.GetNumAvailableBytes() == 0)
					{
						flush((uint)_nowPos64);
						return;
					}

					if (_nowPos64 - progressPosValuePrev >= (1 << 12))
					{
						_finished = false;
						finished = false;
						return;
					}
				}
			}
		}

		void releaseMFStream()
		{
			if (_matchFinder != null && _needReleaseMFStream)
			{
				_matchFinder.ReleaseStream();
				_needReleaseMFStream = false;
			}
		}

		void setOutStream(System.IO.Stream outStream) { _rangeEncoder.SetStream(outStream); }
		void releaseOutStream() { _rangeEncoder.ReleaseStream(); }

		void releaseStreams()
		{
			releaseMFStream();
			releaseOutStream();
		}

		void setStreams(System.IO.Stream inStream, System.IO.Stream outStream)
		{
			_inStream = inStream;
			_finished = false;
			create();
			setOutStream(outStream);
			init();

			// if (!_fastMode)
			{
				fillDistancesPrices();
				fillAlignPrices();
			}

			_lenEncoder.SetTableSize(_numFastBytes + 1 - Base.kMatchMinLen);
			_lenEncoder.UpdateTables((uint)1 << _posStateBits);
			_repMatchLenEncoder.SetTableSize(_numFastBytes + 1 - Base.kMatchMinLen);
			_repMatchLenEncoder.UpdateTables((uint)1 << _posStateBits);

			_nowPos64 = 0;
		}


		public void Code(System.IO.Stream inStream, System.IO.Stream outStream,
			long inSize, long outSize, ICodeProgress progress)
		{
			_needReleaseMFStream = false;
			try
			{
				setStreams(inStream, outStream);
				while (true)
				{
					CodeOneBlock(out long processedInSize, out long processedOutSize, out bool finished);
					if (finished)
						return;
					if (progress != null)
					{
						progress.SetProgress(processedInSize, processedOutSize);
					}
				}
			}
			finally
			{
				releaseStreams();
			}
		}
		
		readonly byte[] _properties = new byte[kPropSize];

		public void WriteCoderProperties(System.IO.Stream outStream)
		{
			_properties[0] = (byte)((_posStateBits * 5 + _numLiteralPosStateBits) * 9 + _numLiteralContextBits);
			for (int i = 0; i < 4; i++)
				_properties[1 + i] = (byte)((_dictionarySize >> (8 * i)) & 0xFF);
			outStream.Write(_properties, 0, kPropSize);
		}

		readonly uint[] _tempPrices = new uint[Base.kNumFullDistances];
		uint _matchPriceCount;

		void fillDistancesPrices()
		{
			for (uint i = Base.kStartPosModelIndex; i < Base.kNumFullDistances; i++)
			{
				uint posSlot = getPosSlot(i);
				int footerBits = (int)((posSlot >> 1) - 1);
				uint baseVal = ((2 | (posSlot & 1)) << footerBits);
				_tempPrices[i] = BitTreeEncoder.ReverseGetPrice(_posEncoders, 
					baseVal - posSlot - 1, footerBits, i - baseVal);
			}

			for (uint lenToPosState = 0; lenToPosState < Base.kNumLenToPosStates; lenToPosState++)
			{
				uint posSlot;
				BitTreeEncoder encoder = _posSlotEncoder[lenToPosState];

				uint st = (lenToPosState << Base.kNumPosSlotBits);
				for (posSlot = 0; posSlot < _distTableSize; posSlot++)
					_posSlotPrices[st + posSlot] = encoder.GetPrice(posSlot);
				for (posSlot = Base.kEndPosModelIndex; posSlot < _distTableSize; posSlot++)
					_posSlotPrices[st + posSlot] += ((((posSlot >> 1) - 1) - Base.kNumAlignBits) << RangeCoder.BitEncoder.kNumBitPriceShiftBits);

				uint st2 = lenToPosState * Base.kNumFullDistances;
				uint i;
				for (i = 0; i < Base.kStartPosModelIndex; i++)
					_distancesPrices[st2 + i] = _posSlotPrices[st + i];
				for (; i < Base.kNumFullDistances; i++)
					_distancesPrices[st2 + i] = _posSlotPrices[st + getPosSlot(i)] + _tempPrices[i];
			}
			_matchPriceCount = 0;
		}

		void fillAlignPrices()
		{
			for (uint i = 0; i < Base.kAlignTableSize; i++)
				_alignPrices[i] = _posAlignEncoder.ReverseGetPrice(i);
			_alignPriceCount = 0;
		}

		static int findMatchFinder(string s)
		{
			for (int m = 0; m < kMatchFinderIDs.Length; m++)
				if (s == kMatchFinderIDs[m])
					return m;
			return -1;
		}
	
		public void SetCoderProperties(CoderPropID[] propIDs, object[] properties)
		{
			for (uint i = 0; i < properties.Length; i++)
			{
				object prop = properties[i];
				switch (propIDs[i])
				{
					case CoderPropID.NumFastBytes:
					{
						if (!(prop is int))
							throw new InvalidParamException();
							int numFastBytes = (int)prop;
						if (numFastBytes < 5 || numFastBytes > Base.kMatchMaxLen)
							throw new InvalidParamException();
						_numFastBytes = (uint)numFastBytes;
						break;
					}
					case CoderPropID.Algorithm:
					{
						break;
					}
					case CoderPropID.MatchFinder:
					{
						if (!(prop is string))
							throw new InvalidParamException();
						EMatchFinderType matchFinderIndexPrev = _matchFinderType;
						int m = findMatchFinder(((string)prop).ToUpper());
						if (m < 0)
							throw new InvalidParamException();
						_matchFinderType = (EMatchFinderType)m;
						if (_matchFinder != null && matchFinderIndexPrev != _matchFinderType)
							{
							_dictionarySizePrev = 0xFFFFFFFF;
							_matchFinder = null;
							}
						break;
					}
					case CoderPropID.DictionarySize:
					{
						const int kDicLogSizeMaxCompress = 30;
						if (!(prop is int))
							throw new InvalidParamException(); ;
							int dictionarySize = (int)prop;
						if (dictionarySize < (uint)(1 << Base.kDicLogSizeMin) ||
							dictionarySize > (uint)(1 << kDicLogSizeMaxCompress))
							throw new InvalidParamException();
						_dictionarySize = (uint)dictionarySize;
						int dicLogSize;
						for (dicLogSize = 0; dicLogSize < (uint)kDicLogSizeMaxCompress; dicLogSize++)
							if (dictionarySize <= ((uint)(1) << dicLogSize))
								break;
						_distTableSize = (uint)dicLogSize * 2;
						break;
					}
					case CoderPropID.PosStateBits:
					{
						if (!(prop is int))
							throw new InvalidParamException();
							int v = (int)prop;
						if (v < 0 || v > (uint)Base.kNumPosStatesBitsEncodingMax)
							throw new InvalidParamException();
						_posStateBits = (int)v;
						_posStateMask = (((uint)1) << (int)_posStateBits) - 1;
						break;
					}
					case CoderPropID.LitPosBits:
					{
						if (!(prop is int))
							throw new InvalidParamException();
							int v = (int)prop;
						if (v < 0 || v > (uint)Base.kNumLitPosStatesBitsEncodingMax)
							throw new InvalidParamException();
						_numLiteralPosStateBits = (int)v;
						break;
					}
					case CoderPropID.LitContextBits:
					{
						if (!(prop is int))
							throw new InvalidParamException();
							int v = (int)prop;
						if (v < 0 || v > (uint)Base.kNumLitContextBitsMax)
							throw new InvalidParamException(); ;
						_numLiteralContextBits = (int)v;
						break;
					}
					case CoderPropID.EndMarker:
					{
						if (!(prop is bool))
							throw new InvalidParamException();
						setWriteEndMarkerMode((bool)prop);
						break;
					}
					default:
						throw new InvalidParamException();
				}
			}
		}
	}
}