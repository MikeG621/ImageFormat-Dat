// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;
	public partial class Decoder
	{
		class LenDecoder
		{
			BitDecoder _choice = new BitDecoder();
			BitDecoder _choice2 = new BitDecoder();
			readonly BitTreeDecoder[] _lowCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
			readonly BitTreeDecoder[] _midCoder = new BitTreeDecoder[Base.kNumPosStatesMax];
			readonly BitTreeDecoder _highCoder = new BitTreeDecoder(Base.kNumHighLenBits);
			uint _numPosStates = 0;

			public void Create(uint numPosStates)
			{
				for (uint posState = _numPosStates; posState < numPosStates; posState++)
				{
					_lowCoder[posState] = new BitTreeDecoder(Base.kNumLowLenBits);
					_midCoder[posState] = new BitTreeDecoder(Base.kNumMidLenBits);
				}
				_numPosStates = numPosStates;
			}

			public void Init()
			{
				_choice.Init();
				for (uint posState = 0; posState < _numPosStates; posState++)
				{
					_lowCoder[posState].Init();
					_midCoder[posState].Init();
				}
				_choice2.Init();
				_highCoder.Init();
			}

			public uint Decode(RangeCoder.Decoder rangeDecoder, uint posState)
			{
				if (_choice.Decode(rangeDecoder) == 0)
					return _lowCoder[posState].Decode(rangeDecoder);
				else
				{
					uint symbol = Base.kNumLowLenSymbols;
					if (_choice2.Decode(rangeDecoder) == 0)
						symbol += _midCoder[posState].Decode(rangeDecoder);
					else
					{
						symbol += Base.kNumMidLenSymbols;
						symbol += _highCoder.Decode(rangeDecoder);
					}
					return symbol;
				}
			}
		}
	}
}