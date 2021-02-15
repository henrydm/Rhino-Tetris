using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Rhino.Display;
using Rhino.Geometry;

namespace RhinoTetris
{
    /// <summary>
    /// Represents a tetris block <see cref="BlockType"/> which has the filled "boxes" info <see cref="Structure"/> and the display mesh <see cref="Mesh"/>
    /// </summary>
    internal class Block
    {
        /// <summary>
        /// Defines the type of the shape of a tetris block.
        /// </summary>
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

        /// <summary>
        /// Ctor. Create a new already filled tetris block.
        /// </summary>
        /// <param name="structure">The distribution of the boxes.</param>
        /// <param name="colorStructure">The color of the block.</param>
        private Block(bool[,] structure, DisplayMaterial[,] colorStructure)
        {
            Structure = structure;
            Colors = colorStructure;
            Mesh = new Mesh();
            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (!Structure[i, j]) continue;

                    var block = Settings.Shape.DuplicateMesh();
                    block.Transform(Settings.Transforms[i, j]);
                    Mesh.Append(block);
                }
            }
            var target = new Point3d(Settings.Columns - 2, 0, 3);
            Mesh.Translate(target - Mesh.GetBoundingBox(true).Center);
        }

        /// <summary>
        /// Ctor. Create a new empty tetris block defined by it's dimensions.
        /// </summary>
        /// <param name="columns">The number of the columns of this block.</param>
        /// <param name="rows">The number of the rows of this block.</param>
        private Block(int columns, int rows) : this(new bool[columns, rows], new DisplayMaterial[columns, rows])
        {
        }

        /// <summary>
        /// Get an empty block with the dimesions of the full game area definded in <see cref="Settings.Columns"/>, <see cref="Settings.Rows"/>
        /// </summary>
        internal static Block Empty => new Block(Settings.Columns, Settings.Rows);

        /// <summary>
        /// Get the display materials for this block.
        /// </summary>
        internal DisplayMaterial[,] Colors { get; }

        /// <summary>
        /// Get this block mesh representation.
        /// </summary>
        internal Mesh Mesh { get; }

        /// <summary>
        /// Get the binary structure for this block.
        /// </summary>
        internal bool[,] Structure { get; }

        /// <summary>
        /// Generate a new random block structure between the tetris options <see cref="BlockType"/>
        /// </summary>
        /// <returns>A new random block.</returns>
        internal static Block GetNextBlock()
        {
            return GetBlockStructure((BlockType)new Random().Next(Enum.GetValues(typeof(BlockType)).Length));
        }

        /// <summary>
        /// Check which lines of the block has all the boxes filled. 
        /// </summary>
        /// <returns>The indices of the full lines.</returns>
        internal List<int> CheckForFullLines()
        {
            var ret = new List<int>();
            for (int i = 0; i <Settings.Rows; i++)
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

        /// <summary>
        /// Check if this block collides with another.
        /// </summary>
        /// <param name="other">Other block to check.</param>
        /// <returns>True if any part of the two blocks are in the same position.</returns>
        internal bool Collide(Block other)
        {
            return Collision(Structure, other.Structure);
        }

        /// <summary>
        /// Get the first position material of this block.
        /// </summary>
        /// <returns>The display Material.</returns>
        internal DisplayMaterial GetFirstElementMatrial()
        {
            foreach (var displayMaterial in Colors)
            {
                if (displayMaterial != null) return displayMaterial;
            }
            return null;
        }

        /// <summary>
        /// Get the maximum X coordinate (game units, not Rhino coordinates) of this block. 
        /// </summary>
        /// <returns>The X Max unit.</returns>
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

        /// <summary>
        /// Get the maximum Y coordinate (game units, not Rhino coordinates) of this block. 
        /// </summary>
        /// <returns>The Max X unit.</returns>
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

        /// <summary>
        /// Get the minimum X coordinate (game units, not Rhino coordinates) of this block. 
        /// </summary>
        /// <returns>The Min X unit.</returns>
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

        /// <summary>
        /// Get the minimum Y coordinate (game units, not Rhino coordinates) of this block. 
        /// </summary>
        /// <returns>The Min Y unit.</returns>
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

        /// <summary>
        /// Merge this block with another one (this block stays invariable).
        /// </summary>
        /// <param name="other">Other block to merge with this one.</param>
        /// <returns>The union of two blocks.</returns>
        internal Block Merge(Block other)
        {
            var newStruct = MergeStructure(other.Structure);
            var newColors = MergeColors(other.Colors);
            var ret = new Block(newStruct, newColors);
            return ret;
        }

        /// <summary>
        /// Move down all the rows above the <see cref="i"/> and keeps the top row empty. 
        /// </summary>
        /// <param name="i">Row number to remove,</param>
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

        /// <summary>
        /// Rotate this block (and translate if it's touching the game limits and the new width is larger than the old one) It doesn't modify this block. "/>
        /// </summary>
        /// <returns>The rotated (and translated if needed) block.</returns>
        internal Block Rotate()
        {
            var newBlock = new Block(Settings.Columns, Settings.Rows);

            var pointsOriginal = GetPoints();
            var points = new List<Point3d>(pointsOriginal);
            var material = Colors[(int)points[0].X, (int)points[0].Z];

            //Avoid square
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
                    p.Transform(Transform.Translation(new Vector3d(Settings.Columns - (int)Math.Round(bbox.Max.X) - 1, 0, 0)));
                    points[i] = p;
                }
            }
            if (bbox.Max.Z >= Settings.Rows)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    var p = new Point3d(points[i]);
                    p.Transform(Transform.Translation(new Vector3d(0, 0, Settings.Rows - (int)Math.Round(bbox.Max.Z) - 1)));
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

        /// <summary>
        /// Translate a block by a given coordinate if possible (clamped to game limits)  It doesn't modify this block.
        /// </summary>
        /// <param name="x">Column position.</param>
        /// <param name="y">Row position.</param>
        /// <returns>The block translated.</returns>
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

        /// <summary>
        /// Check if any part of these blocks is coincident.
        /// </summary>
        /// <param name="a">First block to check.</param>
        /// <param name="b">Second block to check.</param>
        /// <returns>True if the position is filled in both blocks.</returns>
        private static bool Collision(bool[,] a, bool[,] b)
        {
            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (a[i, j] && b[i, j]) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get a tetris typical block structure
        /// </summary>
        /// <param name="type">Tetris shape type.</param>
        /// <returns>The block for the passed shape.</returns>
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

        /// <summary>
        /// Get a 3D baricenter point by average.
        /// </summary>
        /// <param name="points">Points to test.</param>
        /// <returns>Barimetric center.</returns>
        private static Point3d GetCenterPoint(IReadOnlyCollection<Point3d> points)
        {
            var center = points.Aggregate(Point3d.Origin, (current, p) => current + p) / points.Count;
            return new Point3d((int)Math.Round(center.X, 0), (int)Math.Round(center.Y, 0), (int)Math.Round(center.Z, 0));
        }

        /// <summary>
        /// Get a tetris block type predefined color.
        /// </summary>
        /// <param name="type">The type of the block to retrieve the color.</param>
        /// <returns>The color to  assing to the block,</returns>
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

        /// <summary>
        /// Get a color array for the given structure only for the filled positions.
        /// </summary>
        /// <param name="structure">Block structure to read from.</param>
        /// <param name="color">Color to fill the new structure.</param>
        /// <returns>The array with the materials in the target positions. (pointers not copies)</returns>
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

        /// <summary>
        /// Get Rhino points for this structure.
        /// </summary>
        /// <returns>The Rhino points for this structure.</returns>
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

        /// <summary>
        /// Perform a transform to this block whatever is it's current position to be centered (X-axis) and on the top (Y-Axis) It doesn't modify this block.
        /// </summary>
        /// <returns>The transformed block./returns>
        private Block InitialTransform()
        {
            var maxX = GetMaxX();
            var maxY = GetMaxY();

            var maxXHalf = maxX / 2;
            var xMotion = maxXHalf + Settings.Columns / 2 - maxXHalf - 1;
            var yMotion = Settings.Rows - maxY - 1;
            return Translate(xMotion, yMotion);
        }

        /// <summary>
        /// Merge this block color structure with another block colors structure, the other overides the used position color in case of collision. It doesn't modify this block.
        /// </summary>
        /// <param name="other">Other material structure.</param>
        /// <returns>The merged material structure.</returns>
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

        /// <summary>
        /// Merge this data struture (positions) with another one. It doesn't modify this block.
        /// </summary>
        /// <param name="other">The other block position structure to merge,</param>
        /// <returns>The new merged positions structure.</returns>
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
    }
}