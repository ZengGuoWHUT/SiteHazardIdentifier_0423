namespace VoxelUI
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string meshPath = "";
            string voxSize = "";
            string voxHeight = "";
            string savePath= "";
            switch (args.Length)
            {
                default:
                    break;
                case 3:
                    meshPath = args[0];
                    voxSize = args[1];
                    voxHeight = args[2];
                    break;
                case 4:
                    meshPath = args[0];
                    voxSize = args[1];
                    voxHeight = args[2];
                    savePath = args[3];
                    break;

            }
            if(args.Length != 0)
            {
                meshPath = args[0];
                voxSize = args[1];
                voxHeight = args[2];
            }
           
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(meshPath,voxSize,voxHeight,savePath));
        }
    }
}