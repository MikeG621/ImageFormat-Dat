// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.RangeCoder
{
	struct BitTreeEncoder
	{
		readonly BitEncoder[] _models;
		readonly int _numBitLevels;

		public BitTreeEncoder(int numBitLevels)
		{
			_numBitLevels = numBitLevels;
			_models = new BitEncoder[1 << numBitLevels];
		}

		public void Init()
		{
			for (uint i = 1; i < (1 << _numBitLevels); i++)
				_models[i].Init();
		}

		public void Encode(Encoder rangeEncoder, uint symbol)
		{
			uint m = 1;
			for (int bitIndex = _numBitLevels; bitIndex > 0; )
			{
				bitIndex--;
				uint bit = (symbol >> bitIndex) & 1;
				_models[m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
			}
		}

		public void ReverseEncode(Encoder rangeEncoder, uint symbol)
		{
			uint m = 1;
			for (uint i = 0; i < _numBitLevels; i++)
			{
				uint bit = symbol & 1;
				_models[m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
				symbol >>= 1;
			}
		}

		public uint GetPrice(uint symbol)
		{
			uint price = 0;
			uint m = 1;
			for (int bitIndex = _numBitLevels; bitIndex > 0; )
			{
				bitIndex--;
				uint bit = (symbol >> bitIndex) & 1;
				price += _models[m].GetPrice(bit);
				m = (m << 1) + bit;
			}
			return price;
		}

		public uint ReverseGetPrice(uint symbol)
		{
			uint price = 0;
			uint m = 1;
			for (int i = _numBitLevels; i > 0; i--)
			{
				uint bit = symbol & 1;
				symbol >>= 1;
				price += _models[m].GetPrice(bit);
				m = (m << 1) | bit;
			}
			return price;
		}

		public static uint ReverseGetPrice(BitEncoder[] Models, uint startIndex,
			int NumBitLevels, uint symbol)
		{
			uint price = 0;
			uint m = 1;
			for (int i = NumBitLevels; i > 0; i--)
			{
				uint bit = symbol & 1;
				symbol >>= 1;
				price += Models[startIndex + m].GetPrice(bit);
				m = (m << 1) | bit;
			}
			return price;
		}

		public static void ReverseEncode(BitEncoder[] Models, uint startIndex,
			Encoder rangeEncoder, int NumBitLevels, uint symbol)
		{
			uint m = 1;
			for (int i = 0; i < NumBitLevels; i++)
			{
				uint bit = symbol & 1;
				Models[startIndex + m].Encode(rangeEncoder, bit);
				m = (m << 1) | bit;
				symbol >>= 1;
			}
		}
	}
}
