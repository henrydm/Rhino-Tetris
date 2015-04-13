using System;
using System.Collections.Generic;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;
using Rhino.Input;
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
        public override string EnglishName
        {
            get { return "Tetris"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {

            var game = new Game();
            game.StartGame();
            return Result.Success;
        }
    }
}
