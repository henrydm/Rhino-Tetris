using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;

namespace RhinoTetris
{
    [System.Runtime.InteropServices.Guid("627a7eed-f1d1-457d-b5f7-514ebba5ab28")]
    public class TetrisCommand : Command
    {
        /// <summary>
        /// The only instance of this command.
        /// </summary>
        public static TetrisCommand Instance { get; private set; }

        /// <summary>
        /// The command name as it appears on the Rhino command line.
        /// </summary>
        public override string EnglishName => "Tetris";

        /// <summary>
        /// Ctor. Rhino is taking care to instantiate it at startup, dont' use it
        /// </summary>
        public TetrisCommand()
        {
            Instance = this;
        }

        /// <summary>
        /// Runs the tetris command.
        /// </summary>
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (Game.Playing)
            {
                Game.Stop();
                return Result.Cancel;
            }

            var options = new GetOption();
            options.SetCommandPrompt("Rhino-Tetris");

            var indexSound = options.AddOption("Sound");
            var indexFx = options.AddOption("FX");
            var indexReset = options.AddOption("Reset");
            var indexExit = options.AddOption("Exit");

            Game.Start();
            Game.OnStopGame += (o, e) => RhinoApp.SendKeystrokes("!", true);
            while (Game.Playing)
            {
                options.Get();
                var slectedOption = options.Option();

                if (slectedOption?.Index == indexFx)
                {
                    Game.Fx = !Game.Fx;
                }
                else if (slectedOption?.Index == indexSound)
                {
                    Game.SetMusic(!Game.Music);
                }
                else if (slectedOption?.Index == indexReset)
                {
                    Game.ResetGame();
                }
                else if (slectedOption?.Index == indexExit)
                {
                    Game.Stop();
                }
            }

            return Result.Success;
        }
    }
}