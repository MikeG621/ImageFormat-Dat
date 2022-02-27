// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.RangeCoder
{
	struct BitEncoder
	{
		public const int kNumBitModelTotalBits = 11;
		public const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
#pragma warning disable IDE1006 // Naming Styles
		const int kNumMoveBits = 5;
		const int kNumMoveReducingBits = 2;
#pragma warning restore IDE1006 // Naming Styles
		public const int kNumBitPriceShiftBits = 6;

		uint _prob;

		public void Init() { _prob = kBitModelTotal >> 1; }

		public void Encode(Encoder encoder, uint symbol)
		{
			uint newBound = (encoder.Range >> kNumBitModelTotalBits) * _prob;
			if (symbol == 0)
			{
				encoder.Range = newBound;
				_prob += (kBitModelTotal - _prob) >> kNumMoveBits;
			}
			else
			{
				encoder.Low += newBound;
				encoder.Range -= newBound;
				_prob -= (_prob) >> kNumMoveBits;
			}
			if (encoder.Range < Encoder.kTopValue)
			{
				encoder.Range <<= 8;
				encoder.ShiftLow();
			}
		}

		static readonly uint[] _probPrices = new uint[kBitModelTotal >> kNumMoveReducingBits];

		static BitEncoder()
		{
			const int kNumBits = (kNumBitModelTotalBits - kNumMoveReducingBits);
			for (int i = kNumBits - 1; i >= 0; i--)
			{
				uint start = (uint)1 << (kNumBits - i - 1);
				uint end = (uint)1 << (kNumBits - i);
				for (uint j = start; j < end; j++)
					_probPrices[j] = ((uint)i << kNumBitPriceShiftBits) +
						(((end - j) << kNumBitPriceShiftBits) >> (kNumBits - i - 1));
			}
		}

		public uint GetPrice(uint symbol)
		{
			return _probPrices[(((_prob - symbol) ^ ((-(int)symbol))) & (kBitModelTotal - 1)) >> kNumMoveReducingBits];
		}
		public uint GetPrice0() { return _probPrices[_prob >> kNumMoveReducingBits]; }
		public uint GetPrice1() { return _probPrices[(kBitModelTotal - _prob) >> kNumMoveReducingBits]; }
	}
}
