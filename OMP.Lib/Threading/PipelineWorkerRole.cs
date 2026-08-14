namespace OMP.Lib.Threading;

internal enum PipelineWorkerRole
{
    Demux,
    AudioDecode,
    AudioPump,
    Video,
    VideoRender,
    Subtitle,
    Session
}
