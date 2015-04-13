using System;
using System.Drawing;
using Rhino.Display;

namespace RhinoTetris
{
    internal class Block
    {
        private enum BlockType
        {
            Square,
            Tri,
            L,
            Line
        };
        public bool[,] Structure { get; private set; }
        public DisplayMaterial[,] Colors { get; private set; }
       

        private Block(bool[,] structure, DisplayMaterial[,] colorStructure)
        {
            Structure = structure;
            Colors = colorStructure;
        }

        private Block(int columns, int rows)
        {
            Structure = new bool[columns, rows];
            Colors = new DisplayMaterial[columns, rows];
        }

        internal bool Collide(Block other)
        {
            return Collision(Structure, other.Structure);
        }

        internal int GetMinY()
        {
            for (int i = 0; i < Settings.Rows; i++)
            {
                for (int j = 0; j < Settings.Columns; j++)
                {
                    if (Structure[j, i]) return i;
                }
            }
            return -1;

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
            var newStructure = new bool[Settings.Columns, Settings.Rows];
            var newColorStructure = new DisplayMaterial[Settings.Columns, Settings.Rows];
            for (var i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (Structure[i, j])
                    {
                        newStructure[i + x, j + y] = true;
                        newColorStructure[i + x, j + y] = Colors[i, j];
                    }
                }
            }
            return new Block(newStructure,newColorStructure);

        }

        private Block InitialTransform()
        {
            var maxX = 0;
            var maxY = 0;

            for (var i = 0; i < Settings.Columns; i++)
            {
                for (var j = 0; j < Settings.Rows; j++)
                {
                    if (!Structure[i, j]) continue;
                    if (i > maxX) maxX = i;
                    if (j > maxY) maxY = j;
                }
            }

            var maxXHalf = maxX / 2;
            var xMotion = maxXHalf + Settings.Columns / 2 - maxXHalf-1;
            var yMotion = Settings.Rows - maxY-1;
            return Translate(xMotion, yMotion);
        }

        internal static Block Empty
        {
            get
            {
                return new Block(Settings.Columns,Settings.Rows);
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
                case BlockType.Line:
                    structure[0, 0] = true;
                    structure[1, 0] = true;
                    structure[2, 0] = true;
                    structure[3, 0] = true;
                    break;
            }
            var colors = GetColorArray(structure);
            var block = new Block(structure, colors);
            return block.InitialTransform();
        }

        internal static Block GetNextBlock()
        {
            return GetBlockStructure((BlockType)new Random().Next(Enum.GetValues(typeof(BlockType)).Length));
        }



        private static DisplayMaterial[,] GetColorArray(bool[,] structure)
        {
            var colorArray = new DisplayMaterial[Settings.Columns, Settings.Rows];

            var color = GetRandomColor();
            for (var i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    if (structure[i, j])
                        colorArray[i, j] = color;
                    else
                        colorArray[i, j] = null;
                }
            }
            return colorArray;
        }

        private static DisplayMaterial GetRandomColor()
        {
            Random randomGen = new Random();
            KnownColor[] names = (KnownColor[])Enum.GetValues(typeof(KnownColor));
            KnownColor randomColorName = names[randomGen.Next(names.Length)];
            Color randomColor = Color.FromKnownColor(randomColorName);
            return new DisplayMaterial(randomColor);
        }
    }
}
