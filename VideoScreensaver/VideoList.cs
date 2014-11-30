using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoScreensaver
{
    [Serializable]
    class VideoList : ArrayList
    {
        public ArrayList ShuffleFilePaths()
        {
            Random random = new Random();

            List<KeyValuePair<int, string>> list = new List<KeyValuePair<int, string>>(this.Count);

            // Add all strings from array
            // Add new random int each time
            foreach (string s in this)
            {
                list.Add(new KeyValuePair<int, string>(random.Next(), s));
            }

            // Sort the list by the random number
            var sorted = from item in list
                         orderby item.Key
                         select item;

            // Allocate new string array
            ArrayList result = new ArrayList(this.Count);

            // Copy values to array
            foreach (KeyValuePair<int, string> pair in sorted)
            {
                result.Add(pair.Value);
            }
            // Return copied array
            return result;
        }

    }
}
