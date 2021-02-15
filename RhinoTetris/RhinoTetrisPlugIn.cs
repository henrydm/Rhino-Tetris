namespace RhinoTetris
{
    public class RhinoTetrisPlugIn : Rhino.PlugIns.PlugIn
    {
        ///<summary>
        ///Gets the only instance of the RhinoTetrisPlugIn plug-in.
        ///</summary>
        public static RhinoTetrisPlugIn Instance { get; private set; }

        /// <summary>
        /// Rhino is taking care to instantiate it by reflection, don't use it.
        /// </summary>
        public RhinoTetrisPlugIn()
        {
            Instance = this;
        }
    }
}