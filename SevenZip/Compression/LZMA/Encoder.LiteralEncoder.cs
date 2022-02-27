// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	using RangeCoder;
	public partial class Encoder
	{
		class LiteralEncoder
		{
			public struct Encoder2
			{
				BitEncoder[] _encoders;

				public void Create() { _encoders = new BitEncoder[0x300]; }

				public void Init() { for (int i = 0; i < 0x300; i++) _encoders[i].Init(); }

				public void Encode(RangeCoder.Encoder rangeEncoder, byte symbol)
				{
					uint context = 1;
					for (int i = 7; i >= 0; i--)
					{
						uint bit = (uint)((symbol >> i) & 1);
						_encoders[context].Encode(rangeEncoder, bit);
						context = (context << 1) | bit;
					}
				}

				public void EncodeMatched(RangeCoder.Encoder rangeEncoder, byte matchByte, byte symbol)
				{
					uint context = 1;
					bool same = true;
					for (int i = 7; i >= 0; i--)
					{
						uint bit = (uint)((symbol >> i) & 1);
						uint state = context;
						if (same)
						{
							uint matchBit = (uint)((matchByte >> i) & 1);
							state += ((1 + matchBit) << 8);
							same = (matchBit == bit);
						}
						_encoders[state].Encode(rangeEncoder, bit);
						context = (context << 1) | bit;
					}
				}

				public uint GetPrice(bool matchMode, byte matchByte, byte symbol)
				{
					uint price = 0;
					uint context = 1;
					int i = 7;
					if (matchMode)
					{
						for (; i >= 0; i--)
						{
							uint matchBit = (uint)(matchByte >> i) & 1;
							uint bit = (uint)(symbol >> i) & 1;
							price += _encoders[((1 + matchBit) << 8) + context].GetPrice(bit);
							context = (context << 1) | bit;
							if (matchBit != bit)
							{
								i--;
								break;
							}
						}
					}
					for (; i >= 0; i--)
					{
						uint bit = (uint)(symbol >> i) & 1;
						price += _encoders[context].GetPrice(bit);
						context = (context << 1) | bit;
					}
					return price;
				}
			}

			Encoder2[] _coders;
			int _numPrevBits;
			int _numPosBits;
			uint _posMask;

			public void Create(int numPosBits, int numPrevBits)
			{
				if (_coders != null && _numPrevBits == numPrevBits && _numPosBits == numPosBits)
					return;
				_numPosBits = numPosBits;
				_posMask = ((uint)1 << numPosBits) - 1;
				_numPrevBits = numPrevBits;
				uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
				_coders = new Encoder2[numStates];
				for (uint i = 0; i < numStates; i++)
					_coders[i].Create();
			}

			public void Init()
			{
				uint numStates = (uint)1 << (_numPrevBits + _numPosBits);
				for (uint i = 0; i < numStates; i++)
					_coders[i].Init();
			}

			public Encoder2 GetSubCoder(uint pos, byte prevByte)
			{ return _coders[((pos & _posMask) << _numPrevBits) + (uint)(prevByte >> (8 - _numPrevBits))]; }
		}
	}
}