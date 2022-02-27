// Part of the LZMA SDK by Igor Pavlov

namespace SevenZip.Compression.LZMA
{
	public partial class Encoder
	{
		class Optimal
		{
			public Base.State State;

			public bool Prev1IsChar;
			public bool Prev2;

			public uint PosPrev2;
			public uint BackPrev2;

			public uint Price;
			public uint PosPrev;
			public uint BackPrev;

			public uint Backs0;
			public uint Backs1;
			public uint Backs2;
			public uint Backs3;

			public void MakeAsChar() { BackPrev = 0xFFFFFFFF; Prev1IsChar = false; }
			public void MakeAsShortRep() { BackPrev = 0; ; Prev1IsChar = false; }
			public bool IsShortRep() { return (BackPrev == 0); }
		};
	}
}