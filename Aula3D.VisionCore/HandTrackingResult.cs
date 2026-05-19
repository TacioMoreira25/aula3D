using System.Collections.Generic;

namespace Aula3D.VisionCore
{
    public class HandData
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public List<Landmark> Landmarks { get; set; } = new();

        // Propriedades calculadas/auxiliares
        public bool IsOpen { get; set; }
        public bool IsPointing { get; set; }
    }

    public class Landmark
    {
        public float Y { get; set; }
    }
}
