// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;
	public partial class Decoder
	{
		class LiteralDecoder
		{
			struct Decoder2
			{
				BitDecoder[] _decoders;
				public void Create() { _decoders = new BitDecoder[0x300]; }
				public void Init() { for (int i = 0; i < 0x300; i++) _decoders[i].Init(); }

				public byte DecodeNormal(RangeCoder.Decoder rangeDecoder)
				{
					uint symbol = 1;
					do
						symbol = (symbol << 1) | _decoders[symbol].Decode(rangeDecoder);
					while (symbol < 0x100);
					return (byte)symbol;
				}

				public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, byte matchByte)
				{
					uint symbol = 1;
					do
					{
						uint matchBit = (uint)(matchByte >> 7) & 1;
						matchByte <<= 1;
						uint bit = _decoders[((1 + matchBit) << 8) + symbol].Decode(rangeDecoder);
						symbol = (symbol << 1) | bit;
						if (matchBit != bit)
						{
							while (symbol < 0x100)
								symbol = (symbol << 1) | _decoders[symbol].Decode(rangeDecoder);
							break;
						}
					}
					while (symbol < 0x100);
					return (byte)symbol;
				}
			}

			Decoder2[] _coders;
			int _numPrevBits;
			int _numPosBits;
			uint _posMask;

			public void Create(int numPosBits, int numPrevBits)
			{
				if (_coders != null && _numPrevBits == numPrevBits &&
					_numPosBits == numPosBits)
					return;
				_numPosBits = numPosBits;
				_posMask = ((uint)1 << numPosBits) - 1;
				_numPrevBits = numPrevBits;
				uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
				_coders = new Decoder2[numStates];
				for (uint i = 0; i < numStates; i++)
					_coders[i].Create();
			}

			public void Init()
			{
				uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
				for (uint i = 0; i < numStates; i++)
					_coders[i].Init();
			}

			uint getState(uint pos, byte prevByte) { return ((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits)); }

			public byte DecodeNormal(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte) { return _coders[getState(pos, prevByte)].DecodeNormal(rangeDecoder); }

			public byte DecodeWithMatchByte(RangeCoder.Decoder rangeDecoder, uint pos, byte prevByte, byte matchByte) { return _coders[getState(pos, prevByte)].DecodeWithMatchByte(rangeDecoder, matchByte); }
		}
	}
}
