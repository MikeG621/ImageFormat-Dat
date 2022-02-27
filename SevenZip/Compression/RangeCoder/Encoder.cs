// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.RangeCoder
{
	class Encoder
	{
		public const uint kTopValue = (1 << 24);

		System.IO.Stream _stream;

		public ulong Low;
		public uint Range;
		uint _cacheSize;
		byte _cache;

		long _startPosition;

		public void SetStream(System.IO.Stream stream)
		{
			_stream = stream;
		}

		public void ReleaseStream()
		{
			_stream = null;
		}

		public void Init()
		{
			_startPosition = _stream.Position;

			Low = 0;
			Range = 0xFFFFFFFF;
			_cacheSize = 1;
			_cache = 0;
		}

		public void FlushData()
		{
			for (int i = 0; i < 5; i++)
				ShiftLow();
		}

		public void FlushStream()
		{
			_stream.Flush();
		}

		public void ShiftLow()
		{
			if ((uint)Low < 0xFF000000 || (uint)(Low >> 32) == 1)
			{
				byte temp = _cache;
				do
				{
					_stream.WriteByte((byte)(temp + (Low >> 32)));
					temp = 0xFF;
				}
				while (--_cacheSize != 0);
				_cache = (byte)(((uint)Low) >> 24);
			}
			_cacheSize++;
			Low = ((uint)Low) << 8;
		}

		public void EncodeDirectBits(uint v, int numTotalBits)
		{
			for (int i = numTotalBits - 1; i >= 0; i--)
			{
				Range >>= 1;
				if (((v >> i) & 1) == 1)
					Low += Range;
				if (Range < kTopValue)
				{
					Range <<= 8;
					ShiftLow();
				}
			}
		}

		public long GetProcessedSizeAdd()
		{
			return _cacheSize +
				_stream.Position - _startPosition + 4;
		}
	}
}