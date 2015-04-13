using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rhino.Geometry;

namespace RhinoTetris
{
    internal static class Settings
    {
        internal static double Width { get; private set; }
        internal static double Heigth { get; private set; }
        internal static double Depth { get; private set; }
        internal static int Rows { get; private set; }
        internal static int Columns { get; private set; }
        internal static int Wait { get; private set; }
        internal static Mesh Shape { get; private set; }
        internal static Transform[,] Transforms { get; private set; }
        static Settings()
        {      
            Rows = 14;
            Columns = 10;

            Width = Rows;
            Heigth = Columns;
            Depth = Width / Convert.ToDouble(Rows);

            var interval = new Interval(-0.5, 0.5);
            Shape = Mesh.CreateFromBox(new Box(Plane.WorldXY, interval, interval, interval), 1, 1, 1);
            Wait = 1000;
            CreateTransforms();
        }

        private static void CreateTransforms()
        {
            var halfWidth = Width / 2.0;
            var halfHeight = Heigth / 2.0;

            Transforms = new Transform[Columns, Rows];
            for (int i = 0; i < Columns; i++)
                for (int j = 0; j < Rows; j++)
                {

                    var x = i - halfWidth + 0.5;
                    var y = j - halfHeight + 0.5;
                    Transforms[i, j] = Transform.Translation(x, y, 0);
                }
        }
    }
}
