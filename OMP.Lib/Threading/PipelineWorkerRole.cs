namespace OMP.Lib.Threading;

internal enum PipelineWorkerRole
{
    Demux,
    Audio,
    Video,
    VideoRender,
    Subtitle
}
