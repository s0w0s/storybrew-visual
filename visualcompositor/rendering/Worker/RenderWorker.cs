using System.Threading.Channels;
using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Backend;
using VisualCompositor.Rendering.Sampling;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Async render worker. Consumes RenderRequests and produces RenderResults.
/// UI only accepts results where result.Revision == state.Revision.
/// During scrub, old requests are discarded and expired requests are cancelled.</summary>
public sealed class RenderWorker : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly Channel<RenderRequest> _requestChannel;
    private readonly CancellationTokenSource _disposeCts = new();
    private Task? _workerTask;

    public RenderWorker(IRenderBackend backend)
    {
        _backend = backend;
        _requestChannel = Channel.CreateBounded<RenderRequest>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public void Start()
    {
        _workerTask = Task.Run(ProcessLoop);
    }

    /// <summary>Submit a render request. If a previous request is still pending, it is discarded.</summary>
    public async Task SubmitAsync(RenderRequest request)
    {
        await _requestChannel.Writer.WriteAsync(request, _disposeCts.Token);
    }

    /// <summary>Try to get the latest result. Returns null if no result is ready.</summary>
    public RenderResult? TryGetResult(long expectedRevision)
    {
        // Results are delivered via callback/event, not polling
        // This is a simplified synchronous check
        return null;
    }

    /// <summary>Event raised when a render result is ready. UI checks result.Revision == state.Revision.</summary>
    public event Action<RenderResult>? ResultReady;

    private async Task ProcessLoop()
    {
        await foreach (var request in _requestChannel.Reader.ReadAllAsync(_disposeCts.Token))
        {
            if (request.CancellationToken.IsCancellationRequested)
                continue;

            try
            {
                var result = await RenderAsync(request);
                ResultReady?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled during scrub
            }
            catch (Exception ex)
            {
                ResultReady?.Invoke(new RenderResult
                {
                    Revision = request.Revision,
                    Time = request.Time,
                    Diagnostics = new List<RenderDiagnostic>
                    {
                        new() { Code = "RENDER_ERROR", Message = ex.Message },
                    },
                });
            }
        }
    }

    private async Task<RenderResult> RenderAsync(RenderRequest request)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resolutionScale = GetResolutionScale(request.Quality);
        _backend.BeginFrame(resolutionScale);

        var spritesToRender = LayerSelector.SelectSpritesForRendering(
            request.Document, request.SelectedSpriteIds, request.MaxRenderedLayers);

        foreach (var sprite in spritesToRender)
        {
            if (request.CancellationToken.IsCancellationRequested)
            {
                request.CancellationToken.ThrowIfCancellationRequested();
            }

            var transform = SpriteSampler.SampleAtTime(sprite, request.Time);
            var texture = await _backend.LoadTextureAsync(sprite.TexturePath);

            _backend.DrawQuad(
                texture,
                transform.Position,
                transform.Scale,
                transform.Rotation,
                transform.Opacity,
                transform.Color,
                transform.Additive);
        }

        var frame = _backend.EndFrame();
        sw.Stop();

        return new RenderResult
        {
            Revision = request.Revision,
            Time = request.Time,
            Frame = frame,
            RenderTime = sw.Elapsed,
        };
    }

    private static float GetResolutionScale(RenderQuality quality) => quality switch
    {
        RenderQuality.FullPreview => 1.0f,
        RenderQuality.InteractiveScrub => 0.5f,
        RenderQuality.FastScrub => 0.25f,
        RenderQuality.TimelineThumbnail => 0.125f,
        _ => 1.0f,
    };

    public void Dispose()
    {
        _disposeCts.Cancel();
        _requestChannel.Writer.TryComplete();
        _workerTask?.Wait(TimeSpan.FromSeconds(5));
        _disposeCts.Dispose();
    }
}
