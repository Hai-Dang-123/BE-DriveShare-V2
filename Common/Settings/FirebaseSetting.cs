using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Settings
{
    public class FirebaseSetting
    {
        public string BucketName { get; set; }            
        public string TokenPath { get; set; }            
        public bool IsProduction { get; set; } = false;  // false = dev-test, true = production
    }
}
