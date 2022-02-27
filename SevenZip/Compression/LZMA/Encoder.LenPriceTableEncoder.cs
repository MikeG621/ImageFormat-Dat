// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	public partial class Encoder
	{
		class LenPriceTableEncoder : LenEncoder
		{
			readonly uint[] _prices = new uint[Base.kNumLenSymbols << Base.kNumPosStatesBitsEncodingMax];
			uint _tableSize;
			readonly uint[] _counters = new uint[Base.kNumPosStatesEncodingMax];

			public void SetTableSize(uint tableSize) { _tableSize = tableSize; }

			public uint GetPrice(uint symbol, uint posState)
			{
				return _prices[posState * Base.kNumLenSymbols + symbol];
			}

			void updateTable(uint posState)
			{
				SetPrices(posState, _tableSize, _prices, posState * Base.kNumLenSymbols);
				_counters[posState] = _tableSize;
			}

			public void UpdateTables(uint numPosStates)
			{
				for (uint posState = 0; posState < numPosStates; posState++)
					updateTable(posState);
			}

			public new void Encode(RangeCoder.Encoder rangeEncoder, uint symbol, uint posState)
			{
				base.Encode(rangeEncoder, symbol, posState);
				if (--_counters[posState] == 0)
					updateTable(posState);
			}
		}
	}
}