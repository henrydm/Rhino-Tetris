using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;

namespace RhinoTetris
{
    [System.Runtime.InteropServices.Guid("627a7eed-f1d1-457d-b5f7-514ebba5ab28")]
    public class Tetris : Command
    {
        public Tetris()
        {
            // Rhino only creates one instance of each command class defined in a
            // plug-in, so it is safe to store a refence in a static property.
            Instance = this;
        }

        ///<summary>The only instance of this command.</summary>
        public static Tetris Instance
        {
            get;
            private set;
        }

        ///<returns>The command name as it appears on the Rhino command line.</returns>
        public override string EnglishName => "Tetris";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            if (Game.Playing) return Result.Failure;

            var options = new GetOption();
            options.SetCommandPrompt("Rhino-Pong");
            var indexSound = options.AddOption("Sound");
            var indexFx = options.AddOption("FX");
            var indexReset = options.AddOption("Reset");
            var indexExit = options.AddOption("Exit");

            var game = new Game { StartingAnimationEnabled = true };
            game.StartGame();
            game.OnStopGame += (o, e) => RhinoApp.SendKeystrokes("!", true);
            while (true)
            {
                options.Get();
                var slectedOption = options.Option();
                if (slectedOption == null) break;

                if (slectedOption.Index == indexFx)
                {
                    game.Fx = !game.Fx;
                }
                else if (slectedOption.Index == indexSound)
                {
                    game.SetMusic(!game.Music);
                }
                else if (slectedOption.Index == indexReset)
                {
                    Game.Playing = false;
                    var music = game.Music;
                    var fx = game.Fx;
                    System.Threading.Thread.Sleep(100);
                    game = new Game { StartingAnimationEnabled = false, Fx = fx };
                    game.OnStopGame += (o, e) => RhinoApp.SendKeystrokes("!", true);
                    game.StartGame();
                    System.Threading.Thread.Sleep(50);
                    if (!music)
                        game.SetMusic(false);
                }
                else
                {
                    break;
                }
            }
            Game.Playing = false;
            return Result.Success;
        }
    }
}