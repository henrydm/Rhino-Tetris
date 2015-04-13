using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using Rhino.Display;

namespace RhinoTetris
{
    class Game
    {
        private Block _gameBlock;
        private Block _currentBlock;
        private BackgroundWorker _bw;
        private bool _playing;
        private void Init()
        {
            _gameBlock = Block.Empty;
            _bw = new BackgroundWorker();
            _bw.DoWork += GameLoop;
            Rhino.Display.DisplayPipeline.CalculateBoundingBox += DisplayPipeline_CalculateBoundingBox;
            Rhino.Display.DisplayPipeline.PostDrawObjects += DisplayPipeline_PostDrawObjects;

        }


        public void StartGame()
        {
            Init();
            _bw.RunWorkerAsync();
        }
        void GameLoop(object sender, DoWorkEventArgs e)
        {
            _playing = true;

            while (_playing)
            {
                if (_currentBlock == null)
                    _currentBlock = Block.GetNextBlock();


                MoveDownOrMerge(_currentBlock);
                RhinoDoc.ActiveDoc.Views.Redraw();
                Thread.Sleep(Settings.Wait);
            }
        }

        private void MoveDownOrMerge(Block block)
        {
            var minY = _currentBlock.GetMinY();
            if (minY == 0)
            {
                _gameBlock = _gameBlock.Merge(block);
                _currentBlock = null;
            }
            else
            {
                _currentBlock = block.Translate(0, -1);

                if (_gameBlock.Collide(_currentBlock))
                {
                    _gameBlock = _gameBlock.Merge(block);
                    _currentBlock = null;
                }
            }
            //else
            //{
            //    _gameBlock = _gameBlock.Merge(newBlock);
            //}


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
            DrawBlocks(new[] { _gameBlock, _currentBlock }, e);
        }

        void DisplayPipeline_CalculateBoundingBox(object sender, CalculateBoundingBoxEventArgs e)
        {

        }
    }
}
