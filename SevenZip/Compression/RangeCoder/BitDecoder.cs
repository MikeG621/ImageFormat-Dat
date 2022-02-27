// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.RangeCoder
{
	struct BitDecoder
	{
		public const int kNumBitModelTotalBits = 11;
		public const uint kBitModelTotal = (1 << kNumBitModelTotalBits);
#pragma warning disable IDE1006 // Naming Styles
		const int kNumMoveBits = 5;
#pragma warning restore IDE1006 // Naming Styles

		uint _prob;

		public void Init() { _prob = kBitModelTotal >> 1; }

		public uint Decode(Decoder rangeDecoder)
		{
			uint newBound = (rangeDecoder.Range >> kNumBitModelTotalBits) * _prob;
			if (rangeDecoder.Code < newBound)
			{
				rangeDecoder.Range = newBound;
				_prob += (kBitModelTotal - _prob) >> kNumMoveBits;
				if (rangeDecoder.Range < Decoder.kTopValue)
				{
					rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
					rangeDecoder.Range <<= 8;
				}
				return 0;
			}
			else
			{
				rangeDecoder.Range -= newBound;
				rangeDecoder.Code -= newBound;
				_prob -= (_prob) >> kNumMoveBits;
				if (rangeDecoder.Range < Decoder.kTopValue)
				{
					rangeDecoder.Code = (rangeDecoder.Code << 8) | (byte)rangeDecoder.Stream.ReadByte();
					rangeDecoder.Range <<= 8;
				}
				return 1;
			}
		}
	}
}
