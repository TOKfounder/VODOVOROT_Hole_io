mergeInto(LibraryManager.library,
{
	InitCloud_js: function()
	{
		var returnStr = cloudSaves;
		var bufferSize = lengthBytesUTF8(returnStr) + 1;
		var buffer = _malloc(bufferSize);
		stringToUTF8(returnStr, buffer, bufferSize);
		return buffer;
	},
	
	LoadCloud_js: function () {
        LoadCloud();
    },	

	SaveCloud_js: function (jsonData, flush)
	{
        var stage2Root = typeof window !== 'undefined' ? window : globalThis;
        if (stage2Root.__vodovorotStage2AuthPending || stage2Root.__vodovorotStage2AuthBlocked) return;
		SaveCloud(UTF8ToString(jsonData), flush);
	}
});