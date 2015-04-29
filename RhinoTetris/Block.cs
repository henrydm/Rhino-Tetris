using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoTetris
{
    internal class Block
    {

        private enum BlockType
        {
            Square,
            Tri,
            L,
            LReverse,
            S,
            SReverse,
            Line
        };
        internal bool[,] Structure { get; private set; }
        internal DisplayMaterial[,] Colors { get; private set; }


        internal Block Rotate()
        {
            var newBlock = new Block(Settings.Columns, Settings.Rows);

            var pointsOriginal = GetPoints();
            var points = new List<Point3d>(pointsOriginal);
            var material = Colors[(int)points[0].X, (int)points[0].Z];

            var center = GetCenterPoint(points);

            for (int i = 0; i < points.Count; i++)
            {
                var p = new Point3d(points[i]);
                p.Transform(Transform.Rotation(Math.PI / 2.0, Vector3d.YAxis, center));
                points[i] = p;
            }


            var bbox = new BoundingBox(points);
            if (bbox.Min.X < 0)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var p = new Point3d(points[i]);
                    p.Transform(Transform.Translation(new Vector3d(Math.Abs(bbox.Min.X), 0, 0)));
                    points[i] = p;
                }
            }
            if (bbox.Min.Z < 0)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var p = new Point3d(points[i]);
                    p.Transform(Transform.Translation(new Vector3d(0, 0, Math.Abs(bbox.Min.Z))));
                    points[i] = p;
                }
            }
            if (bbox.Max.X >= Settings.Columns)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var p = new Point3d(points[i]);
                    p.Transform(Transform.Translation(new Vector3d((int)bbox.Max.X - Settings.Columns - 1, 0, 0)));
                    points[i] = p;
                }
            }
            if (bbox.Max.Z >= Settings.Rows)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var p = new Point3d(points[i]);
                    p.Transform(Transform.Translation(new Vector3d(0, 0, (int)bbox.Max.Z - Settings.Rows - 2)));
                    points[i] = p;
                }
            }


            for (int column = 0; column < Settings.Columns; column++)
            {
                for (int row = 0; row < Settings.Rows; row++)
                {
                    var strucPoint = new Point3d(column, 0, row);
                    if (points.Contains(strucPoint))
                    {
                        newBlock.Structure[column, row] = true;
                        newBlock.Colors[column, row] = material;
                    }
                    else
                    {
                        newBlock.Structure[column, row] = false;
                    }
                }
            }
            return newBlock;

        }
        public Mesh Mesh { get; private set; }
        private static Point3d GetCenterPoint(IReadOnlyCollection<Point3d> points)
        {
            var center = points.Aggregate(Point3d.Origin, (current, p) => current + p) / points.Count;

            return new Point3d((int)Math.Round(center.X, 0), (int)Math.Round(center.Y, 0), (int)Math.Round(center.Z, 0));
            //var dictionary = new Dictionary<double, Point3d>();

            //foreach (var point in points)
            //{
            //    var dist = (point - center).SquareLength;
            //    if (!dictionary.ContainsKey(dist))
            //        dictionary.Add(dist, point);
            //}

            //return dictionary.OrderBy(p => p.Value).ToArray()[0].Value;

        }

        private IEnumerable<Point3d> GetPoints()
        {
            var points = new List<Point3d>();
            for (int column = 0; column < Settings.Columns; column++)
            {
                for (int row = 0; row < Settings.Rows; row++)
                {
                    if (Structure[column, row])
                        points.Add(new Point3d(column, 0, row));
                }
            }
            return points;
        }

        private Block(bool[,] structure, DisplayMaterial[,] colorStructure)
        {
            Structure = structure;
            Colors = colorStructure;
            Mesh = new Mesh();
            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (!Structure[i,j])continue;

                    var block = Settings.Shape.DuplicateMesh();
                    block.Transform(Settings.Transforms[i, j]);
                    Mesh.Append(block);
                }
            }
            var target = new Point3d(Settings.Width-2, 0, 3);
            Mesh.Translate(target - Mesh.GetBoundingBox(true).Center);
        }

        internal DisplayMaterial GetFirstColor()
        {
            foreach (var displayMaterial in Colors)
            {
                if (displayMaterial!=null) return displayMaterial;
            }
            return null;
        }
        private Block(int columns, int rows)
            : this(new bool[columns, rows], new DisplayMaterial[columns, rows])
        {
        }

        internal bool Collide(Block other)
        {
            return Collision(Structure, other.Structure);
        }

        internal int GetMinY()
        {
            for (int row = 0; row < Settings.Rows; row++)
            {
                for (int column = 0; column < Settings.Columns; column++)
                {
                    if (Structure[column, row])
                        return row;
                }
            }
            return 0;

        }
        internal int GetMaxY()
        {
            for (int row = Settings.Rows - 1; row >= 0; row--)
            {
                for (int column = 0; column < Settings.Columns; column++)
                {

                    if (Structure[column, row])
                        return row;
                }
            }
            return 0;

        }
        internal int GetMinX()
        {
            for (int column = 0; column < Settings.Columns; column++)
            {

                for (int row = 0; row < Settings.Rows; row++)
                {

                    if (Structure[column, row])
                    {
                        return column;
                    }
                }
            }
            return 0;

        }
        internal int GetMaxX()
        {
            for (int column = Settings.Columns - 1; column >= 0; column--)
            {
                for (int row = Settings.Rows - 1; row >= 0; row--)
                {
                    if (Structure[column, row])
                    {
                        return column;
                    }
                }
            }
            return 0;

        }

        private static bool Collision(bool[,] a, bool[,] b)
        {
            // return a.Where((t1, i) => a.Where((t, j) => t1[j] && b[i][j]).Any()).Any();

            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (a[i, j] && b[i, j]) return true;
                }
            }
            return false;

        }



        private bool[,] MergeStructure(bool[,] other)
        {

            var ret = new bool[Settings.Columns, Settings.Rows];

            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    ret[i, j] = Structure[i, j] || other[i, j];
                }
            }
            return ret;

        }
        private DisplayMaterial[,] MergeColors(DisplayMaterial[,] other)
        {


            var ret = new DisplayMaterial[Settings.Columns, Settings.Rows];
            for (int i = 0; i < Settings.Columns; i++)
            {

                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (Colors[i, j] != null)
                        ret[i, j] = Colors[i, j];
                    else if (other[i, j] != null)
                        ret[i, j] = other[i, j];
                }
            }
            return ret;

        }

        internal Block Merge(Block other)
        {
            var newStruct = MergeStructure(other.Structure);
            var newColors = MergeColors(other.Colors);
            var ret = new Block(newStruct, newColors);
            return ret;
        }


        internal Block Translate(int x, int y)
        {
            var maxX = GetMaxX();
            var minX = GetMinX();
            var minY = GetMinY();

            if (maxX + x >= Settings.Columns || minX + x < 0 || minY + y < 0) return this;

            var newStructure = new bool[Settings.Columns, Settings.Rows];
            var newColorStructure = new DisplayMaterial[Settings.Columns, Settings.Rows];
            for (var column = 0; column < Settings.Columns; column++)
            {
                for (int row = 0; row < Settings.Rows; row++)
                {
                    if (Structure[column, row])
                    {
                        newStructure[column + x, row + y] = true;
                        newColorStructure[column + x, row + y] = Colors[column, row];
                    }
                }
            }

            return new Block(newStructure, newColorStructure);

        }

        private Block InitialTransform()
        {
            var maxX = GetMaxX();
            var maxY = GetMaxY();

            //for (var column = 0; column < Settings.Columns; column++)
            //{
            //    for (var row = 0; row < Settings.Rows; row++)
            //    {
            //        if (!Structure[column, row]) continue;
            //        if (column > maxX) maxX = column;
            //        if (row > maxY) maxY = row;
            //    }
            //}

            var maxXHalf = maxX / 2;
            var xMotion = maxXHalf + Settings.Columns / 2 - maxXHalf - 1;
            var yMotion = Settings.Rows - maxY - 1;
            return Translate(xMotion, yMotion);
        }

        internal static Block Empty
        {
            get
            {
                return new Block(Settings.Columns, Settings.Rows);
            }
        }

        private static Block GetBlockStructure(BlockType type)
        {
            var structure = new bool[Settings.Columns, Settings.Rows];
            switch (type)
            {
                case BlockType.Square:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[0, 1] = true;
                    structure[1, 1] = true;
                    break;
                case BlockType.Tri:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[2, 0] = true;
                    structure[1, 1] = true;
                    break;
                case BlockType.L:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[0, 1] = true;
                    structure[0, 2] = true;
                    break;
                case BlockType.LReverse:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[1, 1] = true;
                    structure[1, 2] = true;
                    break;
                case BlockType.S:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[1, 1] = true;
                    structure[2, 1] = true;
                    break;
                case BlockType.SReverse:
                    structure[0, 1] = true;
                    structure[1, 1] = true;
                    structure[1, 0] = true;
                    structure[2, 0] = true;
                    break;
                case BlockType.Line:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[2, 0] = true;
                    structure[3, 0] = true;
                    break;
            }

            var colors = GetColorArray(structure, GetColor(type));
            var block = new Block(structure, colors);
            return block.InitialTransform();
        }

        private static Color GetColor(BlockType type)
        {
            switch (type)
            {
                case BlockType.Square:
                    return Color.DarkOliveGreen;
                case BlockType.Tri:
                    return Color.DarkOrchid;
                case BlockType.L:
                    return Color.SaddleBrown;
                case BlockType.LReverse:
                    return Color.RoyalBlue;
                case BlockType.S:
                    return Color.Maroon;
                case BlockType.SReverse:
                    return Color.OrangeRed;
                case BlockType.Line:
                    return Color.Goldenrod;
                default:
                    return Color.Black;
            }
        }

        internal static Block GetNextBlock()
        {
            return GetBlockStructure((BlockType)new Random().Next(Enum.GetValues(typeof(BlockType)).Length));
        }

        internal void RemoveRow(int i)
        {

            for (int row = i; row < Settings.Rows - 1; row++)
            {
                for (int column = 0; column < Settings.Columns; column++)
                {
                    Structure[column, row] = Structure[column, row + 1];
                    Colors[column, row] = Colors[column, row + 1];
                }
            }
        }


        internal List<int> CheckForFullLines()
        {

            var ret = new List<int>();
            for (int i = 0; i < Settings.Rows; i++)
            {
                var all = true;
                for (int j = 0; j < Settings.Columns; j++)
                {
                    if (!Structure[j, i])
                    {
                        all = false;
                        break;
                    }
                }
                if (all) ret.Add(i);
            }

            return ret.Any() ? ret.OrderByDescending(p => p).ToList() : null;

        }

        private static DisplayMaterial[,] GetColorArray(bool[,] structure, Color color)
        {
            var colorArray = new DisplayMaterial[Settings.Columns, Settings.Rows];

            var material = new DisplayMaterial(color);
            for (var i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (structure[i, j])
                        colorArray[i, j] = material;
                    else
                        colorArray[i, j] = null;
                }
            }
            return colorArray;
        }

        private static DisplayMaterial[,] GetColorArray(bool[,] structure)
        {
            return GetColorArray(structure, GetRandomColor());
        }

        private static Color GetRandomColor()
        {
            var randomGen = new Random();
            var names = (KnownColor[])Enum.GetValues(typeof(KnownColor));
            var randomColorName = names[randomGen.Next(names.Length)];
            return Color.FromKnownColor(randomColorName);
        }


    }
}
