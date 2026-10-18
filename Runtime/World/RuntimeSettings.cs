using System.Text.Json.Serialization;

enum ViewProjection { Perspective, Isometric }

sealed record RuntimeSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<ViewProjection>))]
    public ViewProjection Projection { get; set; }
    public float InitialDistance { get; set; } = FollowCamera.DefaultDistance;
    public bool ShowGrid { get; set; } = true;
    public int TargetFps { get; set; } = 60;

    public void Validate()
    {
        if (!Enum.IsDefined(Projection) || !float.IsFinite(InitialDistance) ||
            InitialDistance < FollowCamera.MinDistance || InitialDistance > FollowCamera.MaxDistance ||
            TargetFps is not (0 or 30 or 60 or 120))
            throw new InvalidDataException("Invalid runtime camera, zoom or frame limit.");
    }
}
