using OpenCvSharp;

namespace VisaoComputacionalTeste
{
    // 1. DATA TRANSFER OBJECT (DTO)
    // Representa os resultados do processamento para desacoplar a visualização do cálculo
    public class HandTrackingResult
    {
        public bool HandDetected { get; set; }
        public bool IsHandOpen { get; set; }
        public string? State { get; set; }
        public Point CenterOfMass { get; set; }
        public Rect BoundingRect { get; set; }
        public Point[]? Contour { get; set; }
        public Point[]? DefectPoints { get; set; }
    }
}