using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using Rhino.Runtime;
using WMPLib;
using Color = System.Drawing.Color;

namespace RhinoTetris
{
    /// <summary>
    /// Main game class which takes care to instantiate run and finish the game loop-
    /// </summary>
    internal static class Game
    {
        /// <summary>
        /// Result of a line down movement.
        /// </summary>
        private enum Status { Moved, Merged, Line, Lost, Win }

        /// <summary>
        /// Axis aligned bounding box which represents the whole game dimensions
        /// </summary>
        private static readonly BoundingBox _globalAABB = new BoundingBox(-7, -2, 0, 11, 2, 17);

        /// <summary>
        /// Indicate the key positions (down true) if a line is completed bye the user (_whiteLines) and it's blinking, Initial animation is running (_initialAnimationRunning) and game is running (_playing)
        /// </summary>
        private static bool _downKeyDown, _leftKeyDown, _rightKeyDown, _rotationKeyDown, _rotationDone, _whiteLines, _initialAnimationRunning, _playing;

        /// <summary>
        /// The current Z position of the initial animation.
        /// </summary>
        private static double _initAnimationZ;

        /// <summary>
        /// The lines (completed by the player) which will blink during the animation.
        /// </summary>
        private static List<int> _animationLines;

        /// <summary>
        /// The boundary shape of the initial animation geometry.
        /// </summary>
        private static Brep _introBorder;

        /// <summary>
        /// Rhino display materials to render the game's elements.
        /// </summary>
        private static readonly DisplayMaterial _introDisplayMaterial, _limitsDisplayMaterial;

        /// <summary>
        /// Game's boundary geometry 
        /// </summary>
        private static readonly Mesh _limits;

        /// <summary>
        /// General score and game mechanics variables
        /// </summary>
        private static int _lines, _points, _level, _waitingMilliseconds, _waitingTicks, _playerCurrent;

        /// <summary>
        /// The process whcich "hosts" the game loop.
        /// </summary>
        private static readonly BackgroundWorker _mainBw;

        /// <summary>
        /// Fx sound file paths.
        /// </summary>
        private static readonly string _musicPath, _fxRotatePath, _fxMovePath, _fxPlacePath, _fxLinePath, _fxLostPath, _fxBootUpPath, _fx4LinesPath, _fxLevelUp;

        /// <summary>
        /// The game clock shapes, full block (all pieces in game) current moving down block, and next block to throw,
        /// </summary>
        private static Block _gameBlock, _currentBlock, _nextBlock;

        /// <summary>
        /// The player for the background music.
        /// </summary>
        private static MediaPlayer _playerMusic;

        /// <summary>
        /// Game constant display score labels in Rhino's viewports.
        /// </summary>
        private static readonly Text3d _scorePointsLabel, _scoreLinesLabel, _scoreLevelLabel;

        /// <summary>
        /// Game variable display score info in Rhino's viewports.
        /// </summary>
        private static Text3d _scorePoints, _scoreLines, _scoreLevel, _introText1, _introText2;

        /// <summary>
        /// The color of the score labels and score text in Rhino's viewports.
        /// </summary>
        private static readonly Color _colorLabel, _colorText;

        /// <summary>
        /// The players for the fx sound (Is a must to use <see cref="WindowsMediaPlayer"/> instead <see cref="MediaPlayer"/> due to that sound must be reproduced simultaneously.
        /// </summary>
        private static readonly WindowsMediaPlayer[] _fxPlayers;

        /// <summary>
        /// Trigered when the game stops.
        /// </summary>
        public static event EventHandler OnStopGame;

        /// <summary>
        /// Get if the game is running.
        /// </summary>
        public static bool Playing => _playing;

        /// <summary>
        /// Get or Set if the game's fx sounds are enabled.
        /// </summary>
        public static bool Fx { get; set; }

        /// <summary>
        /// Get or Set if the game's music is enabled,
        /// </summary>
        public static bool Music { get; private set; }

        /// <summary>
        /// Get if the current skin is Panther
        /// </summary>
        private static bool IsPantherSkin => Skin.ActiveSkin == null || Skin.ActiveSkin.ToString() == "PantherSkin.PantherSkin";

        /// <summary>
        /// Static Ctor, Initialize variables
        /// </summary>
        static Game()
        {
            Music = true;
            _animationLines = new List<int>();
            var tempFolder = Path.GetTempPath();
            _musicPath = Path.Combine(tempFolder, "MusicA.wav");
            _fxRotatePath = Path.Combine(tempFolder, "rotate.wav");
            _fxMovePath = Path.Combine(tempFolder, "move.wav");
            _fxPlacePath = Path.Combine(tempFolder, "place.wav");
            _fxLinePath = Path.Combine(tempFolder, "line.wav");
            _fxLostPath = Path.Combine(tempFolder, "lost.wav");
            _fxBootUpPath = Path.Combine(tempFolder, "bootup.wav");
            _fx4LinesPath = Path.Combine(tempFolder, "tetris.wav");
            _fxLevelUp = Path.Combine(tempFolder, "levelUp.wav");

            WriteFile(_musicPath, Resource.MusicA);
            WriteFile(_fxRotatePath, Resource.rotate);
            WriteFile(_fxMovePath, Resource.move);
            WriteFile(_fxPlacePath, Resource.place);
            WriteFile(_fxLinePath, Resource.line);
            WriteFile(_fxLostPath, Resource.lost);
            WriteFile(_fxBootUpPath, Resource.bootUp);
            WriteFile(_fx4LinesPath, Resource._4lines);
            WriteFile(_fxLevelUp, Resource.levelUp);

            _mainBw = new BackgroundWorker { WorkerReportsProgress = true };
            _mainBw.DoWork += GameLoop;
            _mainBw.RunWorkerCompleted += GameFinished;

            _colorLabel = IsPantherSkin ? Color.DimGray : Color.FromArgb(70, 70, 70);
            _colorText = IsPantherSkin ? Color.White : Color.Black;
            _limitsDisplayMaterial = new DisplayMaterial(IsPantherSkin ? Color.FromArgb(100, 0, 0) : Color.DarkSlateGray);
            _introDisplayMaterial = new DisplayMaterial(Color.DimGray);

            _limits = CreateLimits(out _scorePointsLabel, out _scoreLevelLabel, out _scoreLinesLabel);

            _fxPlayers = new WindowsMediaPlayer[15];
            for (int i = 0; i < _fxPlayers.Length; i++)
            {
                _fxPlayers[i] = new WindowsMediaPlayer();
            }
        }

        /// <summary>
        /// Enable or disable in game music.
        /// </summary>
        /// <param name="enable">True to enable, false to disable</param>
        public static void SetMusic(bool enable)
        {
            Music = enable;
            if (_playerMusic == null) return;
            if (enable) _playerMusic.Play();
            else _playerMusic.Stop();
        }

        /// <summary>
        ///  Play the game, if the game is already playing it does nothing.
        /// </summary>
        /// <param name="showInitAnimation">If true an initial animation will be performed before to start the game.</param>
        public static void Start(bool showInitAnimation = true)
        {
            if (_playing) return;

            ResetGame();
            SetUpScene();

            var bw = new BackgroundWorker();
            bw.DoWork += (o, e) =>
            {

                if (showInitAnimation)
                {
                    StartingAnimation();
                }
            };

            bw.RunWorkerCompleted += (o, e) =>
            {
                if (!_playing)
                {
                    GameFinished(null, null);
                    return;
                }

                _playerMusic = new MediaPlayer();
                _playerMusic.Open(new Uri(_musicPath));
                _playerMusic.MediaEnded += PlayerMusic_MediaEnded;
                if (Music) _playerMusic.Play();
                _mainBw.RunWorkerAsync();
                RhinoDoc.ActiveDoc.Views.Redraw();
            };

            bw.RunWorkerAsync();

            RhinoDoc.ActiveDoc.Views.Redraw();
        }

        /// <summary>
        /// Remove all blocks and score.
        /// </summary>
        public static void ResetGame()
        {
            _level = 1;
            _points = 0;
            _lines = 0;
            _waitingMilliseconds = 80;
            _gameBlock = Block.Empty;
            _playing = true;

            CreateText3DPoints();
            CreateText3DLevel();
            CreateText3DLines();
            GenerateBlocks();

            RhinoDoc.ActiveDoc?.Views?.Redraw();
        }

        /// <summary>
        /// Stop the game, if the game is already stoped it does nothing.
        /// </summary>
        public static void Stop()
        {
            _playing = false;
        }

        /// <summary>
        /// Attempts to write a stream to a file path if it doesn't exist.
        /// </summary>
        /// <param name="filePath">File path to write to.</param>
        /// <param name="stream">Stream to write.</param>
        private static void WriteFile(string filePath, Stream stream)
        {
            try
            {
                if (File.Exists(filePath)) return;
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, (int)stream.Length);
                var fs = new FileStream(filePath, FileMode.Create);
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush();
                fs.Close();
            }
            catch (Exception ex)
            {
                WriteCrash(ex, $"Tetris couldn't load sound file:{filePath}");
            }

        }

        /// <summary>
        /// Background music end event. (it will start the music again to play it in an infinite loop)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void PlayerMusic_MediaEnded(object sender, EventArgs e)
        {
            if (!Music) return;
            _playerMusic = new MediaPlayer();
            _playerMusic.Open(new Uri(_musicPath));
            _playerMusic.MediaEnded += PlayerMusic_MediaEnded;
            _playerMusic.Play();
        }

        /// <summary>
        /// Check the incoming line, merge the blocks if needed, updates the score and plays the sound and animations if needed. (this actually it's a game step when a line goes down)
        /// </summary>
        /// <returns>The result of line movement.</returns>
        private static Status CheckScene()
        {
            if (_gameBlock.Collide(_currentBlock))
            {
                return Status.Lost;
            }

            if (_level == 99)
                return Status.Win;

            List<int> fullLines;
            var res = MoveDownOrMerge(_currentBlock, out fullLines);

            if (res != Status.Moved)
                CreateText3DPoints();

            if (res == Status.Line)
            {
                _lines += fullLines.Count;
                SetPoints(fullLines.Count);
                CreateText3DLines();

                _animationLines = fullLines;
                ShowAnimationLines();
                PlayFx(fullLines.Count == 4 ? _fx4LinesPath : _fxLinePath);
                return Status.Line;
            }

            return res;
        }

        /// <summary>
        /// Create the limit geometry of the game.
        /// </summary>
        private static Mesh CreateLimits(out Text3d labelPoints, out Text3d labelLevel, out Text3d labelLines)
        {
            var halfX = Settings.Columns / 2.0;

            //Main board
            var minLeft = new Point3d(-halfX - 1, -0.8, 0);
            var maxLeft = new Point3d(-halfX, 0.8, Settings.Rows);

            var minRight = new Point3d(halfX, -0.8, 0);
            var maxRight = new Point3d(halfX + 1, 0.8, Settings.Rows);

            var left = Mesh.CreateFromBox(new BoundingBox(minLeft, maxLeft), 1, 1, 1);
            var right = Mesh.CreateFromBox(new BoundingBox(minRight, maxRight), 1, 1, 1);

            var mesh = new Mesh();
            mesh.Append(right);
            mesh.Append(left);

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

            mesh.Append(scoreUp);
            mesh.Append(scoreRight);
            mesh.Append(scoreDown);

            var sizeInfoBox = (Settings.Rows - 2.0) / 4.0;

            var minScoreSeparatorUp = new Point3d(maxRight.X, minRight.Y, (sizeInfoBox * 3.0) + 1 - 0.1);
            var maxScoreSeparatorUp = new Point3d(maxRight.X + 4, maxRight.Y, (sizeInfoBox * 3.0) + 1 + 0.1);

            var minScoreSeparatorMidle = new Point3d(maxRight.X, minRight.Y, (sizeInfoBox * 2.0) + 1 - 0.1);
            var maxScoreSeparatorrMidle = new Point3d(maxRight.X + 4, maxRight.Y, (sizeInfoBox * 2.0) + 1 + 0.1);

            var minScoreSeparatorDown = new Point3d(maxRight.X, minRight.Y, sizeInfoBox + 1 - 0.1);
            var maxScoreSeparatorDown = new Point3d(maxRight.X + 4, maxRight.Y, sizeInfoBox + 1 + 0.1);

            var scoreSeparatorUp = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorUp, maxScoreSeparatorUp), 1, 1, 1);
            var scoreSeparatorMid = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorMidle, maxScoreSeparatorrMidle), 1, 1, 1);
            var scoreSeparatorDown = Mesh.CreateFromBox(new BoundingBox(minScoreSeparatorDown, maxScoreSeparatorDown), 1, 1, 1);

            mesh.Append(scoreSeparatorUp);
            mesh.Append(scoreSeparatorMid);
            mesh.Append(scoreSeparatorDown);

            mesh.UnifyNormals();
            mesh.FaceNormals.ComputeFaceNormals();

            var planePoints = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 4.0) - 0.2), Vector3d.XAxis, Vector3d.ZAxis);
            labelPoints = new Text3d("Points", planePoints, 0.6);

            var planeLevel = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 3.0) - 0.2), Vector3d.XAxis, Vector3d.ZAxis);
            labelLevel = new Text3d("Level", planeLevel, 0.6);

            var planeLines = new Plane(new Point3d((maxScoreUp.X + minScoreUp.X) / 2.0 - 1.5, 0, (sizeInfoBox * 2.0) - 0.2), Vector3d.XAxis, Vector3d.ZAxis);
            labelLines = new Text3d("Lines", planeLines, 0.6);

            return mesh;
        }

        /// <summary>
        /// Create the <see cref="_scoreLevel"/> text which represents player reached level on the viewport.
        /// </summary>
        private static void CreateText3DLevel()
        {
            var sizeInfoBox = (Settings.Rows - 2.0) / 4.0;
            var z = sizeInfoBox * 2.5;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLevel = new Text3d(_level.ToString(), plane, 0.9);
            var halfX = (_scoreLevel.BoundingBox.Max.X + _scoreLevel.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Columns / 2.0 + 3 - halfX;
            _scoreLevel.TextPlane = plane;
        }

        /// <summary>
        /// Create the <see cref="_scoreLines"/> text, which represents player lines number on the viewport.
        /// </summary>
        private static void CreateText3DLines()
        {
            var sizeInfoBox = (Settings.Rows - 2.0) / 4.0;
            var z = sizeInfoBox * 1.5;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scoreLines = new Text3d(_lines.ToString(), plane, 0.9);
            var halfX = (_scoreLines.BoundingBox.Max.X + _scoreLines.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Columns / 2.0 + 3 - halfX;
            _scoreLines.TextPlane = plane;
        }

        /// <summary>
        /// Create the <see cref="_scorePoints"/> text, which represents player points on the viewport.
        /// </summary>
        private static void CreateText3DPoints()
        {
            var sizeInfoBox = (Settings.Rows - 2.0) / 4.0;
            var z = sizeInfoBox * 3.5;

            var plane = new Plane(new Point3d(0, 0, z), Vector3d.XAxis, Vector3d.ZAxis);
            _scorePoints = new Text3d(_points.ToString(), plane, 0.9);
            var halfX = (_scorePoints.BoundingBox.Max.X + _scorePoints.BoundingBox.Min.X) / 2.0;
            plane.OriginX = Settings.Columns / 2.0 + 3 - halfX;
            _scorePoints.TextPlane = plane;
        }

        /// <summary>
        /// A static bounding bos is passed to ensure Rhino will draw all the elements in game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {
            e.IncludeBoundingBox(_globalAABB);
        }

        /// <summary>
        /// Draw all game objects on Rhino's viewport
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void DisplayPipeline_PostDrawObjects(object sender, DrawEventArgs e)
        {
            if (_initAnimationZ > 0)
            {
                if (_introBorder != null)
                {
                    e.Display.DrawBrepShaded(_introBorder, _introDisplayMaterial);
                }

                if (_introText1 != null)
                    e.Display.Draw3dText(_introText1, _introDisplayMaterial.Diffuse);
                if (_introText2 != null)
                    e.Display.Draw3dText(_introText2, _introDisplayMaterial.Diffuse);
            }
            else
            {
                DrawBlocks(new[] { _gameBlock, _currentBlock }, e);
                e.Display.DrawMeshShaded(_limits, _limitsDisplayMaterial);
                e.Display.DrawMeshWires(_limits, Color.Black);

                e.Display.Draw3dText(_scorePointsLabel, _colorLabel);
                e.Display.Draw3dText(_scoreLevelLabel, _colorLabel);
                e.Display.Draw3dText(_scoreLinesLabel, _colorLabel);

                e.Display.Draw3dText(_scorePoints, _colorText);
                e.Display.Draw3dText(_scoreLevel, _colorText);
                e.Display.Draw3dText(_scoreLines, _colorText);
            }
        }

        /// <summary>
        /// Draw passed blocks to the passed pipeline.
        /// </summary>
        /// <param name="blocks">Blocks to draw.</param>
        /// <param name="e">Rhino draw arguments with pipeline to draw in.</param>
        private static void DrawBlocks(ICollection<Block> blocks, DrawEventArgs e)
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
            e.Display.DrawMeshShaded(_nextBlock.Mesh, _nextBlock.GetFirstElementMatrial());
            e.Display.DrawMeshWires(_nextBlock.Mesh, Color.Black);
        }

        /// <summary>
        /// Stop the game and the music (non blocking) and releases all linked events.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void GameFinished(object sender, RunWorkerCompletedEventArgs e)
        {
            _playerMusic.Stop();
            SetDownScene();
            RhinoDoc.ActiveDoc?.Views?.Redraw();
        }

        /// <summary>
        /// Blocking method which performs the game logic steps, when game stops this method returns.
        /// </summary>
        /// <param name="sender">Not used</param>
        /// <param name="e">Wraper for the result of the game (win or lose).</param>
        private static void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;
            _waitingTicks = 36;

            var cont = 0;
            var sw = new Stopwatch();
            while (_playing)
            {
                cont++;
                sw.Restart();
                if (_currentBlock == null) continue;

                if (_rotationKeyDown && !_rotationDone)
                {
                    _rotationDone = true;
                    PlayFx(_fxRotatePath);
                    var rotatedBlock = _currentBlock.Rotate();
                    if (!rotatedBlock.Collide(_gameBlock))
                        _currentBlock = rotatedBlock;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (_leftKeyDown)
                {
                    PlayFx(_fxMovePath);

                    var translated = _currentBlock.Translate(-1, 0);
                    if (!_gameBlock.Collide(translated))
                        _currentBlock = translated;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                if (_rightKeyDown)
                {
                    PlayFx(_fxMovePath);

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
                        SetLevel();
                    }

                    RhinoDoc.ActiveDoc.Views.Redraw();
                }

                if (cont >= _waitingTicks)
                {
                    var res = CheckScene();
                    if (res == Status.Lost)
                    {
                        PlayFx(_fxLostPath);
                        _playing = false;
                        e.Result = res;
                        OnStopGame?.Invoke(null, null);
                        break;
                    }
                    if (res == Status.Win)
                    {
                        PlayFx(_fxLostPath);
                        _playing = false;
                        e.Result = res;
                        break;
                    }
                    cont = 0;
                    RhinoDoc.ActiveDoc.Views.Redraw();
                }
                while (sw.ElapsedMilliseconds < _waitingMilliseconds)
                {
                    if (!_playing) return;
                    Thread.Sleep(20);
                    //Thread.Sleep(_waitingMilliseconds - (int)sw.ElapsedMilliseconds);
                }
            }
        }

        /// <summary>
        /// Switch the next block to the current one and generate the next block which the player will get.
        /// </summary>
        private static void GenerateBlocks()
        {
            _currentBlock = _nextBlock ?? Block.GetNextBlock();
            _nextBlock = Block.GetNextBlock();
        }

        /// <summary>
        /// Key presed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void HookManager_KeyDown(object sender, KeyEventArgs e)
        {
            if (_initialAnimationRunning) return;

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
                    _rotationDone = false;
                    _rotationKeyDown = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Space:
                    _rotationDone = false;
                    _rotationKeyDown = true;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Escape:
                    _playing = false;
                    OnStopGame?.Invoke(null, null);
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        /// <summary>
        /// Key released event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void HookManager_KeyUp(object sender, KeyEventArgs e)
        {
            if (_initialAnimationRunning) return;

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
                    _rotationKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;

                case Keys.Space:
                    _rotationKeyDown = false;
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        /// <summary>
        /// Move down a block and merge it with the whole game block, it checks if there is any collision and the game refult of the collision (Moved, Merged or Line-match,
        /// </summary>
        /// <param name="block">Input block to merge.</param>
        /// <param name="lines">Output lines that the user gets with this merge.</param>
        /// <returns>The result of the move down,</returns>
        private static Status MoveDownOrMerge(Block block, out List<int> lines)
        {
            lines = null;
            var minY = _currentBlock.GetMinY();
            if (minY == 0)
            {
                PlayFx(_fxPlacePath);
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
                PlayFx(_fxPlacePath);
                _gameBlock = _gameBlock.Merge(block);
                GenerateBlocks();
                lines = _gameBlock.CheckForFullLines();

                if (lines != null)
                    return Status.Line;
                return Status.Merged;
            }

            return Status.Moved;
        }

        /// <summary>
        /// Play a sound from file path.
        /// </summary>
        /// <param name="uri">Loacation of the path.</param>
        private static void PlayFx(string uri)
        {
            if (Fx) return;
            try
            {
                _playerCurrent++;
                if (_playerCurrent >= _fxPlayers.Length)
                    _playerCurrent = 0;


                _fxPlayers[_playerCurrent].URL = uri;
            }
            catch (Exception ex)
            {
                WriteCrash(ex, $"Tetris couldn't play the sound:{uri}");
                return;
            }
        }

        /// <summary>
        /// Release keyboard events and Rhino drawing event subscriptions 
        /// </summary>
        private static void SetDownScene()
        {
            DisplayPipeline.CalculateBoundingBox -= DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects -= DisplayPipeline_PostDrawObjects;
            KeyBoard.OnKeyDown -= HookManager_KeyDown;
            KeyBoard.OnKeyUp -= HookManager_KeyUp;
            KeyBoard.Stop();
        }

        /// <summary>
        /// Set the waiting time betwen line movement using the current <see cref="_level"/>
        /// </summary>
        private static void SetLevel()
        {
            var level = (_lines / 10) + 1;
            if (_level == level)
            {
                return;
            }

            if (_level > 1)
            { PlayFx(_fxLevelUp); }
            _level = level;

            if (_level < 5)
            {
                _waitingTicks -= (9 - _level * 2);
            }
            else if (_level < 7)
            {
                _waitingTicks -= (9 - _level);
            }
            else
                RhinoMath.Clamp(_waitingTicks -= 2, 6, 200);

            CreateText3DLevel();
        }

        /// <summary>
        /// Set the player points, and update the UI in viewport.
        /// </summary>
        /// <param name="rows">The number of lines that the player made.</param>
        private static void SetPoints(int rows)
        {
            switch (rows)
            {
                case 1:
                    _points += 40;
                    break;

                case 2:
                    _points += 300;
                    break;

                case 3:
                    _points += 300;
                    break;

                case 4:
                    _points += 1000;
                    break;
            }
            CreateText3DPoints();
            SetLevel();
        }

        /// <summary>
        /// 
        /// </summary>
        private static void SetUpScene()
        {
            DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            DisplayPipeline.PostDrawObjects += DisplayPipeline_PostDrawObjects;
            KeyBoard.OnKeyDown += HookManager_KeyDown;
            KeyBoard.OnKeyUp += HookManager_KeyUp;
            KeyBoard.Start();

        }

        /// <summary>
        /// Perform a non blocking flickering in game line animation (when the player complete one or more lines).
        /// </summary>
        private static void ShowAnimationLines()
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

        /// <summary>
        /// Performs a blocking initial game animation.
        /// </summary>
        private static void StartingAnimation()
        {
            _initialAnimationRunning = true;
            try
            {
                _introText1 = new Text3d("Rhinoceros®") { Height = 1.5 };
                _introText2 = new Text3d("Classic Games") { Height = 0.7 };
                _initAnimationZ = 0.01;

                var offsetX = (_introText1.BoundingBox.Max.X - _introText1.BoundingBox.Min.X) / 2.0;

                var leftpt = new Point3d(-5, 0, 0.2);
                var rightpt = new Point3d(5, 0, 0.2);

                var arcLeftSmall = new Arc(new Plane(leftpt, Vector3d.ZAxis, -Vector3d.XAxis), 1.7, Math.PI).ToNurbsCurve();
                var arcLeftBig = new Arc(new Plane(leftpt, Vector3d.ZAxis, -Vector3d.XAxis), 2.1, Math.PI).ToNurbsCurve();

                var arcRightSmall = new Arc(new Plane(rightpt, Vector3d.ZAxis, Vector3d.XAxis), 1.7, Math.PI).ToNurbsCurve();
                var arcRightBig = new Arc(new Plane(rightpt, Vector3d.ZAxis, Vector3d.XAxis), 2.1, Math.PI).ToNurbsCurve();

                var lineUpBigA = new Line(arcLeftBig.PointAtStart, arcRightBig.PointAtStart).ToNurbsCurve();
                var lineBottomBigA = new Line(arcLeftBig.PointAtEnd, arcRightBig.PointAtEnd).ToNurbsCurve();

                var lineUpSmallA = new Line(arcLeftSmall.PointAtStart, arcRightSmall.PointAtStart).ToNurbsCurve();
                var lineBottomSmallB = new Line(arcLeftSmall.PointAtEnd, arcRightSmall.PointAtEnd).ToNurbsCurve();

                var cross = new Line(arcLeftBig.PointAtStart, arcLeftSmall.PointAtStart).ToNurbsCurve();

                var join = Curve.JoinCurves(new[] { arcLeftBig, arcLeftSmall, lineUpBigA, lineBottomBigA, lineUpSmallA, lineBottomSmallB, arcRightSmall, arcRightBig });

                if (join != null && join.Count() == 2)
                {
                    if (join[0].ClosestPoint(cross.PointAtStart, out var crossT))
                        join[0].ChangeClosedCurveSeam(crossT);

                    var sweep = new SweepOneRail { ClosedSweep = true };
                    var breps = sweep.PerformSweep(join[0], cross);
                    if (breps != null && breps.Count() == 1)
                        _introBorder = breps[0];
                }

                var sw = new Stopwatch();
                var iterations = 100;
                var delta = 0.1;
                for (int i = 0; i < iterations; i++)
                {
                    sw.Restart();
                    var skipAnimation = KeyBoard.PressedKey == Keys.Escape || KeyBoard.PressedKey == Keys.Enter || KeyBoard.PressedKey == Keys.Space;

                    if (!_playing || skipAnimation)
                    {
                        _initAnimationZ = 0;
                        return;
                    }

                    _introBorder?.Translate(Point3d.Origin - new Point3d(0, 0, _initAnimationZ));
                    _initAnimationZ += delta;

                    var plane1 = new Plane(new Point3d(-offsetX, 0, _initAnimationZ), Vector3d.XAxis, Vector3d.ZAxis);
                    var plane2 = new Plane(new Point3d(-offsetX + 0.2, 0, _initAnimationZ - 1), Vector3d.XAxis, Vector3d.ZAxis);

                    _introText1.TextPlane = plane1;
                    _introText2.TextPlane = plane2;
                    _introBorder?.Translate(Point3d.Origin - new Point3d(0, 0, -_initAnimationZ));

                    RhinoDoc.ActiveDoc.Views.Redraw();

                    if (sw.ElapsedMilliseconds < 40)
                    {
                        Thread.Sleep(40 - (int)sw.ElapsedMilliseconds);
                    }


                }
                Thread.Sleep(300);
                var fx = new MediaPlayer();
                fx.Open(new Uri(_fxBootUpPath));
                fx.MediaEnded += PlayerMusic_MediaEnded;
                fx.Play();
                Thread.Sleep(1000);
                RhinoDoc.ActiveDoc.Views.Redraw();
                _initAnimationZ = 0;
            }
            finally
            {
                _initialAnimationRunning = false;
            }
        }

        /// <summary>
        /// Writes a crash message and stack trace to the Rhino command line.
        /// </summary>
        /// <param name="ex">Target exception.</param>
        /// <param name="textBefore">Text inserted before the crash message.</param>
        /// <param name="textAfter">Text inserted after the crash message.</param>
        private static void WriteCrash(this Exception ex, string textBefore = null, string textAfter = null)
        {
#if DEBUG
            RhinoApp.WriteLine($"{textBefore}Message:{ex.Message}{Environment.NewLine}{ex.StackTrace}{textAfter}");
#endif
        }
    }
}