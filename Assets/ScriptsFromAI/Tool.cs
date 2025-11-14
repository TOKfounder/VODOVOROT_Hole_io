public static class Tool
{
	public static string ConvertText(int cnt)
	{
		string st = "";
		if (cnt < 1000)
		{
			st = $"{(long)cnt}";
		}
		else if (cnt >= 1000 && cnt < 1000000)
		{
			st = $"{(long)cnt / 1000}.{(long)cnt % 1000 / 10:D2}K";
		}
		else if (cnt >= 1000000 && cnt < 1000000000)
		{
			st = $"{(long)cnt / 1000000}.{(long)cnt % 1000000 / 10000:D2}M";
		}
		return st;
	}
}
