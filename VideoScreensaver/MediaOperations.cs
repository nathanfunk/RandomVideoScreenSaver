using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Windows;
using System.Drawing;

namespace VideoScreensaver
{
    // Handles operations on media files like Delete, Email, Copy, etc.
    class MediaOperations
    {
        static public void RecycleFile(string filePath)
        {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        // Put a file on the clipboard both as a text reprsentation (the path) and an
        // image representation (the bitmap itself).
        static public void CopyToClipboard(string filePath)
        {
            System.Windows.DataObject dataObject = new System.Windows.DataObject();

            // Set the text representation as the path
            dataObject.SetData(System.Windows.DataFormats.Text, filePath, true);

            // Now let's get the image itself

            System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(
                    System.Drawing.Image.FromFile(filePath));

            dataObject.SetData(System.Windows.DataFormats.Bitmap, bmp, true);

            // Then place the dataobject on the clipboard
            System.Windows.Clipboard.SetDataObject(dataObject, true); 
        }
    }
}
