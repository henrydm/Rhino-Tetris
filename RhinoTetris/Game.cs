using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using Rhino;
using Rhino.Display;
using Gma.UserActivityMonitor;
using Rhino.Geometry;
using Color = System.Drawing.Color;

namespace RhinoTetris
{


    class Game
    {
        private enum Status { Moved, Merged, Line, Lost, Win }
        private enum SoundType { Rotate, Move, Place,Line,Lost }
        private int _lines, _points, _level, _blocksWaitingTicks;
        private List<int> _animationLines;
        private Mesh _limits;
        private MediaPlayer _playerMusic;
        private DisplayMaterial _limitsDisplayMaterial;
        private bool _downKeyDown, _leftKeyDown, _rightKeyDown, _upKeyDown, _upKeyReleased, _rotationDone;
        private Block _gameBlock;
        private Block _currentBlock;
        private Block _nextBlock;
        private BackgroundWorker _mainBw;
        private bool _playing, _whiteLines;
        private string _musicPath, _fxRotatePath, _fxMovePath, _fxPlacePath, _fxLinePath,_fxLostPath;
        private Text3d _scorePointsLabel, _scoreLinesLabel, _scoreLevelLabel, _scorePoints, _scoreLines, _scoreLevel;
       
        private static void WriteFile(string filePath, Stream stream)
        {
            //if (!File.Exists(filePath))
            //{
            try
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                var fs = new FileStream(filePath, FileMode.Create);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
                fs.Close();
            }
            catch
            {
                // ignored
            }
            // }
        }


        private void SetUpScene()
        {
            _animationLines = new List<int>();
            var tempFolder = Path.GetTempPath();
            _musicPath = Path.Combine(tempFolder, "MusicA.wav");
            _fxRotatePath = Path.Combine(tempFolder, "rotate.wav");
            _fxMovePath = Path.Combine(tempFolder, "move.wav");
            _fxPlacePath = Path.Combine(tempFolder, "place.wav");
            _fxLinePath = Path.Combine(tempFolder, "line.wav");
            _fxLostPath = Path.Combine(tempFolder, "lost.wav");

            WriteFile(_musicPath, Resource.MusicA);
            WriteFile(_fxRotatePath, Resource.rotate);
            WriteFile(_fxMovePath, Resource.move);
            WriteFile(_fxPlacePath, Resource.place);
            WriteFile(_fxLinePath, Resource.line);
            WriteFile(_fxLostPath, Resource.lost);

            _playerMusic = new MediaPlayer();
            _playerMusic.Open(new Uri(_musicPath));
            _playerMusic.MediaEnded += _playerMusic_MediaEnded;
            _playerMusic.Play();

            _gameBlock = Block.Empty;
            _mainBw = new BackgroundWorker { WorkerReportsProgress = true };
            _mainBw.DoWork += GameLoop;
            _mainBw.ProgressChanged += _mainBw_ProgressChanged;
            _mainBw.RunWorkerCompleted += GameFinished;
            _limitsDisplayMaterial = new DisplayMaterial(Color.IndianRed);
            CreateLimits();
            GenerateBlocks();
            DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects += DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown += HookManager_KeyDown;
            HookManager.KeyUp += HookManager_KeyUp;

            _points = 0;
            _lines = 0;
            SetLevel();

        }
        private void SetDownScene()
        {
            DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects -= DisplayPipeline_PostDrawObjects;
            HookManager.KeyDown -= HookManager_KeyDown;
            HookManager.KeyUp -= HookManager_KeyUp;
        }

        void _playerMusic_MediaEnded(object sender, EventArgs e)
        {
            _playerMusic = new MediaPlayer();
            _playerMusic.Open(new Uri(_musicPath));
            _playerMusic.MediaEnded += _playerMusic_MediaEnded;
            _playerMusic.Play();
        }

        void _mainBw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var type = (SoundType)e.UserState;
            var pl = new MediaPlayer();

            switch (type)
            {
                case SoundType.Rotate:
                    pl.Open(new Uri(_fxRotatePath));
                    break;
                case SoundType.Move:
                    pl.Open(new Uri(_fxMovePath));
                    break;
                case SoundType.Place:
                    pl.Open(new Uri(_fxPlacePath));
                    break;
                case SoundType.Line:
                    pl.Open(new Uri(_fxLinePath));
                    break;
                case SoundType.Lost:
                    pl.Open(new Uri(_fxLostPath));
                    break;
            }
            pl.Play();

        }

        private void CreateLimits()
        {
            var halfX = Settings.Width / 2.0;

            //Main board
            var minLeft = new Point3d(-halfX - 1, -0.8, 0);
            var maxLeft = new Point3d(-halfX, 0.8, Settings.Heigth);

            var minRight = new Point3d(halfX, -0.8, 0);
            var maxRight = new Point3d(halfX + 1, 0.8, Settings.Heigth);

            var minDown = new Point3d(-halfX - 1, -0.8, -1);
            var maxDown = new Point3d(halfX + 1, 0.8, 0);

            var left = Mesh.CreateFromBox(new BoundingBox(minLeft, maxLeft), 1, 1, 1);
            var right = Mesh.CreateFromBox(new BoundingBox(minRight, maxRight), 1, 1, 1);
            //var down = Mesh.CreateFromBox(new BoundingBox(minDown, maxDown), 1, 1, 1);

            _limits = new Mesh();
            _limits.Append(right);
            _limits.Append(left);
            //_limits.Append(down);


            //Score board
            var minScoreUp = new Point3d(maxRight.X, maxRight.Y, maxRight.Z - 1);
            var maxScoreUp = new Point3d(maxRight.X + 5, minRight.Y, maxRight.Z);

            var minScoreDown = new Point3d(maxRight.X, maxRight.Y, 0);
            var maxScoreDown = new Point3d(maxRight.X + 5, minRight.Y, 1);

            var minScoreRight = new Point3d(maxRight.X + 4, minRight.Y, 1);
            var maxScoreRight = new Point3d(maxRight.X + 5, maxRight.Y, maxScoreUp.Z - 1);

            var scoreUp = Mesh.CreateFromBox(new BoundingBox(minScoreUp, maxScoreUp), 1, 1, 1);
            var scoreRight = Mesh.CreateFromBox(new BoundingBox(minScoreRight, maxScoreRight), 1, 1, 1);
            var scoreDown = Mesh.CreateFromBox(new BoundingBox(minScoreDown, maxScoreDown), 1, 1, 1);


            _limits.Append(scoreUp);
            _limits.Append(scoreRight);
            _limits.Append(scoreDown);


            var sizeInfoBox = (Settings.Heigth - 2.0) / 4.0;

            var minScoreSeparatorUp = new Point3d(maxRight.X, minRight.Y, (sizeInfoBox * 3.0) + 1 - 0.1);
            var maxScoreSeparatorUp = new Point3d(maxRight.X + 4, maxRight.Y, (sizeInfoBox * 3.0) + 1 + 0.1);

            var minScoreSeparatorMidle = new Point3d(maxRight.X, minRight.Y, (sizeInfoBox * 2.0) + 1 - 0.1);
            var maxScoreSeparatorrMidle = new Point3d(maxRight.X + 4, maxRight.Y, (sizeInfoBox * 2.0) + 1 + 0.1);

            var minScoreSeparatorDown = new Point3d(maxRight.X, minRight.Y, sizeInfoBox + 1 - 0.1);
            var maxScoreSeparatorDown = new Point3d(maxRight.X + 4, maxRight.Y, sizeInfoBox + 1 + 0.1);


            var scoreSeparatorUp = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorUp, maxScoreSeparatorUp), 1, 1, 1);
            var scoreSeparatorMid = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorMidle, maxScoreSeparatorrMidle), 1, 1, 1);
            var scoreSeparatorDown = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorDown, maxScoreSeparatorDown), 1, 1, 1);

            _limits.Append(scoreSeparatorUp);
            _limits.Append(scoreSeparatorMid);
            _limits.Append(scoreSeparatorDown);


            var planePoints = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 4.0) + 0.3), Vector3d.XAxis, Vector3d.ZAxis);
            _scorePointsLabel = new Text3d("Points", planePoints, 0.5);
            var pointsHalfX = (_scorePointsLabel.BoundingBox.Max.X + _scorePointsLabel.BoundingBox.Min.X) / 2.0;


            var planeLevel = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 3.0) + 0.3), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLevelLabel = new Text3d("Level", planeLevel, 0.5);

            var planeLines = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 2.0) + 0.3), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLinesLabel = new Text3d("Lines", planeLines, 0.5);


            CreateText3DPoints();
            CreateText3DLevel();
            CreateText3DLines();
        }

        private void CreateText3DPoints()
        {
            var sizeInfoBox = (Settings.Heigth - 2.0) / 4.0;
            var z = sizeInfoBox * 3.7;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scorePoints = new Text3d(_points.ToString(), plane, 0.8);
            var halfX = (_scorePoints.BoundingBox.Max.X + _scorePoints.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Width / 2.0 + 3 - halfX;
            _scorePoints.TextPlane = plane;
        }
        private void CreateText3DLevel()
        {
            var sizeInfoBox = (Settings.Heigth - 2.0) / 4.0;
            var z = sizeInfoBox * 2.7;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLevel = new Text3d(_level.ToString(), plane, 0.8);
            var halfX = (_scoreLevel.BoundingBox.Max.X + _scoreLevel.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Width / 2.0 + 3 - halfX;
            _scoreLevel.TextPlane = plane;
        }
        private void CreateText3DLines()
        {

            var sizeInfoBox = (Settings.Heigth - 2.0) / 4.0;
            var z = sizeInfoBox * 1.7;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLines = new Text3d(_lines.ToString(), plane, 0.8);
            var halfX = (_scoreLines.BoundingBox.Max.X + _scoreLines.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Width / 2.0 + 3 - halfX;
            _scoreLines.TextPlane = plane;
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


        public void StartGame()
        {
            SetUpScene();
            RhinoDoc.ActiveDoc.Views.Redraw();
            _mainBw.RunWorkerAsync();
        }

        private void ShowAnimationLines()
        {

            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    _whiteLines = !_whiteLines;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                    Thread.Sleep(150);
                }
                foreach (var fullLine in _animationLines)
                {
                    _gameBlock.RemoveRow(fullLine);
                }
                _animationLines = new List<int>();
                _whiteLines = false;
            };
            bw.RunWorkerAsync();
        }

        void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;

            var cont = 0;
            while (_playing)
            {
                cont++;

                if (_currentBlock == null) continue;

                if (_upKeyDown && !_rotationDone)
                {
                    _mainBw.ReportProgress(0, SoundType.Rotate);
                    _rotationDone = true;
                    var rotatedBlock = _currentBlock.Rotate();
                    if (!rotatedBlock.Collide(_gameBlock))
                        _currentBlock = rotatedBlock;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (_leftKeyDown)
                {
                    _mainBw.ReportProgress(0, SoundType.Move);
                    var translated = _currentBlock.Translate(-1, 0);
                    if (!_gameBlock.Collide(translated))
                        _currentBlock = translated;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                if (_rightKeyDown)
                {
                    _mainBw.ReportProgress(0, SoundType.Move);
                    var translated = _currentBlock.Translate(1, 0);
                    if (!_gameBlock.Collide(translated))
                        _currentBlock = translated;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                if (_downKeyDown)
                {
                    var result = CheckScene();

                    if (result == Status.Moved)
                    {
                        _points++;
                        CreateText3DPoints();
                        SetLevel();
                    }

                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (cont >= _blocksWaitingTicks)
                {
                    var res = CheckScene();
                    if (res == Status.Lost)
                    {
                        _mainBw.ReportProgress(0, SoundType.Lost);
                        _playing = false;
                        e.Result = res;
                        break;
                    }
                    else if (res == Status.Lost)
                    {
                        _mainBw.ReportProgress(0, SoundType.Lost);
                        _playing = false;
                        e.Result = res;
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
            _playerMusic.Stop();
            Thread.Sleep(1500);
            SetDownScene();
            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        private void SetPoints(int rows)
        {
            switch (rows)
            {
                case 1:
                    _points += 40;
                    break;
                case 2:
                    _points += 100;
                    break;
                case 3:
                    _points += 300;
                    break;
                case 4:
                    _points += 600;
                    break;
                case 5:
                    _points += 1000;
                    break;
            }
            CreateText3DPoints();
            SetLevel();
        }

        private void SetLevel()
        {
            _level = (_points / 1000) + 1;
            _blocksWaitingTicks = (20 / _level * 2);
            CreateText3DLevel();
        }

        private Status CheckScene()
        {
            if (_gameBlock.Collide(_currentBlock))
            {
                return Status.Lost;
            }

            if (_level == 15)
                return Status.Win;

            List<int> fullLines;
            var res = MoveDownOrMerge(_currentBlock, out fullLines);

            if (res == Status.Line)
            {
                _lines += fullLines.Count;
                SetPoints(fullLines.Count);
                CreateText3DLines();

                _animationLines = fullLines;
                ShowAnimationLines();
                _mainBw.ReportProgress(0, SoundType.Line);
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
                _mainBw.ReportProgress(0, SoundType.Place);
                _gameBlock = _gameBlock.Merge(block);
                GenerateBlocks();
                lines = _gameBlock.CheckForFullLines();

                if (lines != null)
                    return Status.Line;

                return Status.Merged;
            }

            _currentBlock = block.Translate(0, -1);

            if (_gameBlock.Collide(_currentBlock))
            {
                _mainBw.ReportProgress(0, SoundType.Place);
                _gameBlock = _gameBlock.Merge(block);
                GenerateBlocks();
                lines = _gameBlock.CheckForFullLines();

                if (lines != null)
                    return Status.Line;
                return Status.Merged;
            }

            return Status.Moved;


        }

        private void GenerateBlocks()
        {

            _currentBlock = _nextBlock ?? Block.GetNextBlock();
            _nextBlock = Block.GetNextBlock();
        }
        private void DrawBlocks(ICollection<Block> blocks, DrawEventArgs e)
        {
            //Draw game block
            for (int i = 0; i < Settings.Columns; i++)
            {
                for (int j = 0; j < Settings.Rows; j++)
                {
                    var enabledBlocks = blocks.Where(block => block != null && block.Structure[i, j]).ToList();

                    foreach (var block in enabledBlocks)
                    {
                        if (!block.Structure[i, j]) continue;

                        e.Display.PushModelTransform(Settings.Transforms[i, j]);
                        e.Display.DrawMeshShaded(Settings.Shape, (_whiteLines && _animationLines.Contains(j)) ? new DisplayMaterial(Color.White) : block.Colors[i, j]);
                        if (!(_whiteLines && _animationLines.Contains(j)))
                            e.Display.DrawMeshWires(Settings.Shape, Color.Black);
                        e.Display.PopModelTransform();
                    }


                }
            }
            //Draw next block
            e.Display.DrawMeshShaded(_nextBlock.Mesh, _nextBlock.GetFirstColor());
            e.Display.DrawMeshWires(_nextBlock.Mesh, Color.Black);
        }

        void DisplayPipeline_PostDrawObjects(object sender, DrawEventArgs e)
        {
            DrawBlocks(new[] { _gameBlock, _currentBlock }, e);
            e.Display.DrawMeshShaded(_limits, _limitsDisplayMaterial);

            e.Display.Draw3dText(_scorePointsLabel, Color.Gray);
            e.Display.Draw3dText(_scoreLevelLabel, Color.Gray);
            e.Display.Draw3dText(_scoreLinesLabel, Color.Gray);

            e.Display.Draw3dText(_scorePoints, Color.Black);
            e.Display.Draw3dText(_scoreLevel, Color.Black);
            e.Display.Draw3dText(_scoreLines, Color.Black);
        }
        void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {

        }
    }
}
