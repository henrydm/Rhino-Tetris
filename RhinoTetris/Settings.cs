using Rhino.Geometry;

namespace RhinoTetris
{
    /// <summary>
    /// Defines the bounds of the game objects.
    /// </summary>
    internal static class Settings
    {
        /// <summary>
        /// The number of rows of the game area.
        /// </summary>
        internal static int Rows => 17;

        /// <summary>
        /// The number of rows of the game area.
        /// </summary>
        internal static int Columns => 10;

        /// <summary>
        /// An individual mesh box instance to create tetris blocks.
        /// </summary>
        internal static Mesh Shape { get; private set; }

        /// <summary>
        /// The tranforms for each game box block position.
        /// </summary>
        internal static Transform[,] Transforms { get; private set; }

        /// <summary>
        /// Static Ctor. Create the non constant variables.
        /// </summary>
        static Settings()
        {
            var interval = new Interval(-0.5, 0.5);
            Shape = Mesh.CreateFromBox(new Box(Plane.WorldXY, interval, interval, interval), 1, 1, 1);
            CreateTransforms();
        }

        /// <summary>
        /// Create and fill the <see cref="Transforms"/> field with the corresponding transformations for the box blocks poitions.
        /// </summary>
        private static void CreateTransforms()
        {
            var halfWidth = Columns / 2.0;

            Transforms = new Transform[Columns, Rows];
            for (int column = 0; column < Columns; column++)
                for (int row = 0; row < Rows; row++)
                {
                    var x = column - halfWidth + 0.5;
                    var z = row + 0.5;
                    Transforms[column, row] = Transform.Translation(x, 0, z);
                }
        }
    }
}
