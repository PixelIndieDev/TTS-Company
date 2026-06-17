namespace TTS_Company.Components.Server.Components
{
    internal struct TTSRawResult
    {
        internal bool IsSuccess;
        internal bool IsCancelled;
        internal string Error;
        internal byte[] Pcm;
        internal int SampleRate;

        internal static TTSRawResult Ok(byte[] pcm, int sampleRate) => new TTSRawResult { IsSuccess = true, Pcm = pcm, SampleRate = sampleRate };
        internal static TTSRawResult Failure(string error) => new TTSRawResult { IsSuccess = false, Error = error };
        internal static TTSRawResult Cancelled() => new TTSRawResult { IsSuccess = false, IsCancelled = true };
    }
}
