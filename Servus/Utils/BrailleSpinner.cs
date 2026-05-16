
static class BrailleSpinner
{
	static readonly Int32[] missingDotMasks =
	[
		0x01,
		0x02,
		0x04,
		0x40,
		0x80,
		0x20,
		0x10,
		0x08,
	];

	public static Char FromNumber(Int32 value)
	{
		var index = ((value % missingDotMasks.Length) + missingDotMasks.Length) % missingDotMasks.Length;
		var mask = 0xFF & ~missingDotMasks[index];

		return (Char)(0x2800 + mask);
	}
}
