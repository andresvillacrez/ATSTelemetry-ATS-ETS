namespace ATSTelemetry;

/// <summary>
/// Escala tipográfica del panel. Los tamaños son píxeles virtuales del canvas;
/// el mínimo informativo es <see cref="Caption"/>.
/// </summary>
internal static class TypeScale
{
    public const float Display = 184f; // velocidad
    public const float Hero = 34f;     // cifra principal de panel
    public const float Title = 24f;    // nombre de empresa, carga
    public const float Body = 16f;     // texto de lectura
    public const float Label = 12f;    // etiquetas de sección
    public const float Caption = 11f;  // texto secundario
    public const float Micro = 10f;    // solo decorativo, nunca informativo
}
