using System;
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
        // internal static int Wait { get; private set; }
        internal static Mesh Shape { get; private set; }
        internal static Transform[,] Transforms { get; private set; }
       // internal static Transform[,] TransformsNextBlock { get; private set; }
        static Settings()
        {
            Rows = 17;
            Columns = 10;

            Width = Columns;
            Heigth = Rows;
            Depth = Width / Convert.ToDouble(Rows);

            var interval = new Interval(-0.5, 0.5);
            Shape = Mesh.CreateFromBox(new Box(Plane.WorldXY, interval, interval, interval), 1, 1, 1);

            //  Wait = 1000;
            CreateTransforms();
        }

        private static void CreateTransforms()
        {
            var halfWidth = Width / 2.0;

            Transforms = new Transform[Columns, Rows];
            for (int column = 0; column < Columns; column++)
                for (int row = 0; row < Rows; row++)
                {
                    var x = column - halfWidth + 0.5;
                    var z = row + 0.5;
                    Transforms[column, row] = Transform.Translation(x, 0, z);
                }

            //Next Block Transforms
            //TransformsNextBlock = new Transform[Columns, Rows];
            //for (int column = 0; column < Columns; column++)
            //    for (int row = 0; row < Rows; row++)
            //    {
            //        var x = halfWidth+column-2 ;
            //        var z = row -Rows+4.5;
            //        TransformsNextBlock[column, row] = Transform.Translation(x, 0, z);

            //    }
            
        }
    }
}
