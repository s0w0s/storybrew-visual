using System.Threading.Channels;
using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Abstractions;
using VisualCompositor.Rendering.Sampling;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Async render worker. Consumes RenderRequest, produces RenderResult.
/// UI only accepts results where result.Revision == state.Revision.</summary>
public sealed class RenderWorker : IDisposable
{
    private readonly Channel<RenderRequest> _channel;
    private readonly IRenderer? _renderer;
    private readonly ITextureProvider? _textureProvider;
    private readonly Task _processTask;
    private CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    public int MaxRenderedLayers { get; set; } = 50;

    public RenderWorker(IRenderer? renderer = null, ITextureProvider? textureProvider = null)
    {
        _renderer = renderer;
        _textureProvider = textureProvider;
        _channel = Channel.CreateUnbounded<RenderRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _processTask = Task.Run(ProcessLoop);
    }

    /// <summary>Submit a render request. Discards any pending request (only latest matters during scrub).</summary>
    public void Submit(RenderRequest request)
    {
        // Cancel any in-flight request
        lock (_lock)
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
        }

        // Drain the channel (discard stale requests)
        while (_channel.Reader.TryRead(out _)) { }

        _channel.Writer.TryWrite(request);
    }

    /// <summary>Event raised when a render result is available. UI checks result.Revision == state.Revision.</summary>
    public event Action<RenderResult>? ResultAvailable;

    private async Task ProcessLoop()
    {
        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            if (request.CancellationToken.IsCancellationRequested)
                continue;

            try
            {
                var result = await Task.Run(() => Render(request), request.CancellationToken);
                ResultAvailable?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                // Request was cancelled (superseded by a newer one)
            }
            catch (Exception ex)
            {
                ResultAvailable?.Invoke(new RenderResult
                {
                    Revision = request.Revision,
                    Time = request.Time,
                    Succeeded = false,
                    ErrorMessage = ex.Message,
                });
            }
        }
    }

    private RenderResult Render(RenderRequest request)
    {
        var selector = new SelectedAndContextSelector(MaxRenderedLayers);
        var spritesToRender = selector.Select(request.Document, request.SelectedLayerIds, request.SelectedSpriteIds, request.Time);

        var sampler = new CommandSampler();
        var result = new RenderResult
        {
            Revision = request.Revision,
            Time = request.Time,
            Succeeded = true,
        };

        foreach (var spriteRef in spritesToRender)
        {
            var sprite = spriteRef.Sprite;
            var state = sampler.SampleSpriteState(sprite, request.Time);

            // Check if texture exists
            bool hasTexture = _textureProvider?.HasTexture(sprite.TexturePath) ?? true;
            if (!hasTexture)
            {
                result.Placeholders.Add(new RenderedPlaceholder
                {
                    Position = state.Position,
                    Size = new Vector2(100, 100), // default placeholder size
                    Label = sprite.TexturePath,
                });
            }
            else
            {
                result.Sprites.Add(new RenderedSprite
                {
                    TexturePath = sprite.TexturePath,
                    Position = state.Position,
                    Scale = state.Scale,
                    Rotation = state.Rotation,
                    Opacity = state.Opacity,
                    Color = state.Color,
                    Additive = state.Additive,
                    FlipH = state.FlipH,
                    FlipV = state.FlipV,
                });
            }
        }

        // If we have a concrete renderer, draw to it
        if (_renderer != null)
        {
            var (w, h) = GetResolution(request);
            _renderer.BeginFrame(w, h);
            foreach (var sprite in result.Sprites)
            {
                _renderer.DrawSprite(sprite.TexturePath, sprite.Position, sprite.Scale, sprite.Rotation, sprite.Opacity, sprite.Color, sprite.Additive, sprite.FlipH, sprite.FlipV);
            }
            foreach (var placeholder in result.Placeholders)
            {
                _renderer.DrawPlaceholder(placeholder.Position, placeholder.Size, placeholder.Label);
            }
            _renderer.EndFrame();
        }

        return result;
    }

    private static (int width, int height) GetResolution(RenderRequest request)
    {
        return request.Quality switch
        {
            RenderQuality.FullPreview => (request.Width, request.Height),
            RenderQuality.InteractiveScrub => (request.Width / 2, request.Height / 2),
            RenderQuality.FastScrub => (request.Width / 4, request.Height / 4),
            RenderQuality.TimelineThumbnail => (request.Width / 8, request.Height / 8),
            _ => (request.Width, request.Height),
        };
    }

    public void Dispose()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        _cts.Dispose();
    }
}
