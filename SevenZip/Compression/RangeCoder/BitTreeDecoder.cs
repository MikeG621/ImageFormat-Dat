﻿// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.RangeCoder
{
	struct BitTreeDecoder
	{
		readonly BitDecoder[] _models;
		readonly int _numBitLevels;

		public BitTreeDecoder(int numBitLevels)
		{
			_numBitLevels = numBitLevels;
			_models = new BitDecoder[1 << numBitLevels];
		}

		public void Init()
		{
			for (uint i = 1; i < (1 << _numBitLevels); i++)
				_models[i].Init();
		}

		public uint Decode(Decoder rangeDecoder)
		{
			uint m = 1;
			for (int bitIndex = _numBitLevels; bitIndex > 0; bitIndex--)
				m = (m << 1) + _models[m].Decode(rangeDecoder);
			return m - ((uint)1 << _numBitLevels);
		}

		public uint ReverseDecode(Decoder rangeDecoder)
		{
			uint m = 1;
			uint symbol = 0;
			for (int bitIndex = 0; bitIndex < _numBitLevels; bitIndex++)
			{
				uint bit = _models[m].Decode(rangeDecoder);
				m <<= 1;
				m += bit;
				symbol |= (bit << bitIndex);
			}
			return symbol;
		}

		public static uint ReverseDecode(BitDecoder[] Models, uint startIndex,
			Decoder rangeDecoder, int NumBitLevels)
		{
			uint m = 1;
			uint symbol = 0;
			for (int bitIndex = 0; bitIndex < NumBitLevels; bitIndex++)
			{
				uint bit = Models[startIndex + m].Decode(rangeDecoder);
				m <<= 1;
				m += bit;
				symbol |= (bit << bitIndex);
			}
			return symbol;
		}
	}
}
