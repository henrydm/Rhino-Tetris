using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Rhino;
using Rhino.Display;
using Gma.UserActivityMonitor;
using Rhino.Geometry;

namespace RhinoTetris
{
    class Game
    {
        private int _lines = 0;
        private Mesh _limits;
        private SoundPlayer _playerMusic, _playerFX;
        private DisplayMaterial _limitsDisplayMaterial;
        private bool _downKeyDown, _leftKeyDown, _rightKeyDown, _upKeyDown, _upKeyReleased, _rotationDone;
        private Block _gameBlock;
        private Block _currentBlock;
        private Block _nextBlock;
        private BackgroundWorker _mainBw;
        private bool _playing;
        private void SetUpScene()
        {

            _playerFX = new SoundPlayer();

            _playerMusic = new SoundPlayer { Stream = Resource.MusicA };
            _playerMusic.PlayLooping();

            _gameBlock = Block.Empty;
            _mainBw = new BackgroundWorker();
            _mainBw.DoWork += GameLoop;
            _mainBw.RunWorkerCompleted += GameFinished;
            _limitsDisplayMaterial = new DisplayMaterial(Color.IndianRed);
            CreateLimits();
            GenerateBlocks();
            DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects += DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown += HookManager_KeyDown;
            HookManager.KeyUp += HookManager_KeyUp;
        }

      
        private void SetDownScene()
        {
            DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects -= DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown -= HookManager_KeyDown;
            HookManager.KeyUp -= HookManager_KeyUp;
        }

        private void CreateLimits()
        {
            var halfX = Settings.Width / 2.0;

            var minLeft = new Point3d(-halfX - 1, -0.8, 0);
            var maxLeft = new Point3d(-halfX, 0.8, Settings.Heigth);

            var minRight = new Point3d(halfX, -0.8, 0);
            var maxRight = new Point3d(halfX + 1, 0.8, Settings.Heigth);

            var minDown = new Point3d(-halfX - 1, -0.8, -1);
            var maxDown = new Point3d(halfX + 1, 0.8, 0);

            var left = Mesh.CreateFromBox(new BoundingBox(minLeft, maxLeft), 1, 1, 1);
            var right = Mesh.CreateFromBox(new BoundingBox(minRight, maxRight), 1, 1, 1);
            var down = Mesh.CreateFromBox(new BoundingBox(minDown, maxDown), 1, 1, 1);

            _limits = new Mesh();
            _limits.Append(right);
            _limits.Append(left);
            //_limits.Append(down);
        }

        void HookManager_KeyUp(object sender, KeyEventArgs e)
        {

            switch (e.KeyCode)
            {
                case Keys.Down:
                    _downKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Left:
                    _leftKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Right:
                    _rightKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Up:
                    _rotationDone = false;
                    _upKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;
            }

        }

        void HookManager_KeyDown(object sender, KeyEventArgs e)
        {

            switch (e.KeyCode)
            {
                case Keys.Down:
                    _downKeyDown = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Left:
                    _leftKeyDown = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Right:
                    _rightKeyDown = true;
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Up:

                    _upKeyDown = true;


                    e.SuppressKeyPress = true;
                    break;
            }

        }

        private void PlaySound(UnmanagedMemoryStream stream)
        {
            var temp = Path.GetTempPath();

            //if (!SoundEnabled) return;
            _playerFX.Stream = stream;
            _playerFX.Play();


        }
        private void PlayMusic()
        {
            //if (!SoundEnabled) return;
            //_playerMusic.Stream = Resource.;
            // _playerMusic.Play();
        }
        public void StartGame()
        {
            SetUpScene();
            _mainBw.RunWorkerAsync();
        }
        void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;

            var cont = 0;
            while (_playing)
            {
                cont++;

                if (_upKeyDown && !_rotationDone)
                {
                    PlaySound(Resource.rotate);
                    _rotationDone = true;
                    var rotatedBlock = _currentBlock.Rotate();
                    if (!rotatedBlock.Collide(_gameBlock))
                        _currentBlock = rotatedBlock;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (_leftKeyDown)
                {
                    PlaySound(Resource.move);
                    var translated = _currentBlock.Translate(-1, 0);
                    if (!_gameBlock.Collide(translated))
                        _currentBlock = translated;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                if (_rightKeyDown)
                {
                    PlaySound(Resource.move);
                    var translated = _currentBlock.Translate(1, 0);
                    if (!_gameBlock.Collide(translated))
                        _currentBlock = translated;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                if (_downKeyDown)
                {
                    CheckScene();
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (cont == 10)
                {
                    var res = CheckScene();
                    if (res == Status.Lost)
                    {
                        _playing = false;
                        break;
                    }
                    cont = 0;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                Thread.Sleep(50);
            }
        }
        void GameFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            SetDownScene();
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private Status CheckScene()
        {

            if (_currentBlock == null)
            {
                GenerateBlocks();
                if (_gameBlock.Collide(_currentBlock))
                {
                    return Status.Lost;

                }
            }


            List<int> fullLines;
            var res = MoveDownOrMerge(_currentBlock, out fullLines);

            if (res == Status.Line)
            {
                _lines += fullLines.Count;
                foreach (var fullLine in fullLines)
                {
                    
                    _gameBlock.RemoveRow(fullLine);
                }

                return Status.Line;
            }

            return res;
        }
        private Status MoveDownOrMerge(Block block, out List<int> lines)
        {

            lines = null;
            var minY = _currentBlock.GetMinY();
            if (minY == 0)
            {
                PlaySound(Resource.place);
                _gameBlock = _gameBlock.Merge(block);
                _currentBlock = null;
                lines = _gameBlock.CheckForFullLines();

                if (lines != null)
                    return Status.Line;

                return Status.Merged;
            }

            _currentBlock = block.Translate(0, -1);

            if (_gameBlock.Collide(_currentBlock))
            {
                PlaySound(Resource.place);
                _gameBlock = _gameBlock.Merge(block);
                _currentBlock = null;
                lines = _gameBlock.CheckForFullLines();

                if (lines != null)
                    return Status.Line;
                return Status.Merged;
            }

            return Status.Moved;


        }

        private enum Status { Moved, Merged, Line, Lost }

        private void GenerateBlocks()
        {

            _currentBlock = _nextBlock ?? Block.GetNextBlock();
            _nextBlock = Block.GetNextBlock();
        }

        private static void DrawBlocks(ICollection<Block> blocks, DrawEventArgs e)
        {

            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    var enabledBlocks = blocks.Where(block => block != null && block.Structure[i, j]).ToList();

                    foreach (var block in enabledBlocks)
                    {

                        e.Display.PushModelTransform(Settings.Transforms[i, j]);
                        if (block.Structure[i, j])
                        {
                            e.Display.DrawMeshShaded(Settings.Shape, block.Colors[i, j]);
                        }
                        e.Display.PopModelTransform();

                    }
                }
            }
        }

        void DisplayPipeline_PostDrawObjects(object sender, DrawEventArgs e)
        {

            var fpsStr = String.Format("Lines:{0}", _lines);
                e.Display.Draw2dText(fpsStr, Color.Firebrick, new Point2d(30, 50), false, 30);
            DrawBlocks(new[] { _gameBlock, _currentBlock }, e);
            e.Display.DrawMeshShaded(_limits, _limitsDisplayMaterial);
        }

        void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {

        }
    }
}
