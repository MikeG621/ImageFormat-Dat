// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;
	public partial class Encoder
    {
		class LenEncoder
		{
			BitEncoder _choice = new BitEncoder();
			BitEncoder _choice2 = new BitEncoder();
			readonly BitTreeEncoder[] _lowCoder = new BitTreeEncoder[Base.kNumPosStatesEncodingMax];
			readonly BitTreeEncoder[] _midCoder = new BitTreeEncoder[Base.kNumPosStatesEncodingMax];
			readonly BitTreeEncoder _highCoder = new BitTreeEncoder(Base.kNumHighLenBits);

			public LenEncoder()
			{
				for (uint posState = 0; posState < Base.kNumPosStatesEncodingMax; posState++)
				{
					_lowCoder[posState] = new BitTreeEncoder(Base.kNumLowLenBits);
					_midCoder[posState] = new BitTreeEncoder(Base.kNumMidLenBits);
				}
			}

			public void Init(uint numPosStates)
			{
				_choice.Init();
				_choice2.Init();
				for (uint posState = 0; posState < numPosStates; posState++)
				{
					_lowCoder[posState].Init();
					_midCoder[posState].Init();
				}
				_highCoder.Init();
			}

			public void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
			{
				if (symbol < Base.kNumLowLenSymbols)
				{
					_choice.Encode(rangeEncoder, 0);
					_lowCoder[posState].Encode(rangeEncoder, symbol);
				}
				else
				{
					symbol -= Base.kNumLowLenSymbols;
					_choice.Encode(rangeEncoder, 1);
					if (symbol < Base.kNumMidLenSymbols)
					{
						_choice2.Encode(rangeEncoder, 0);
						_midCoder[posState].Encode(rangeEncoder, symbol);
					}
					else
					{
						_choice2.Encode(rangeEncoder, 1);
						_highCoder.Encode(rangeEncoder, symbol - Base.kNumMidLenSymbols);
					}
				}
			}

			public void SetPrices(uint posState, uint numSymbols, uint[] prices, uint st)
			{
				uint a0 = _choice.GetPrice0();
				uint a1 = _choice.GetPrice1();
				uint b0 = a1 + _choice2.GetPrice0();
				uint b1 = a1 + _choice2.GetPrice1();
				uint i;
				for (i = 0; i < Base.kNumLowLenSymbols; i++)
				{
					if (i >= numSymbols)
						return;
					prices[st + i] = a0 + _lowCoder[posState].GetPrice(i);
				}
				for (; i < Base.kNumLowLenSymbols + Base.kNumMidLenSymbols; i++)
				{
					if (i >= numSymbols)
						return;
					prices[st + i] = b0 + _midCoder[posState].GetPrice(i - Base.kNumLowLenSymbols);
				}
				for (; i < numSymbols; i++)
					prices[st + i] = b1 + _highCoder.GetPrice(i - Base.kNumLowLenSymbols - Base.kNumMidLenSymbols);
			}
		};
	}
}
