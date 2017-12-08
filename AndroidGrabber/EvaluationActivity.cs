using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace AndroidGrabber
{
    [Activity(Label = "EvaluationActivity")]
    public class EvaluationActivity : Activity
    {
        private CancellationTokenSource tokensource = new CancellationTokenSource();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Create your application here
        }

        public override void OnBackPressed()
        {
            tokensource.Cancel();
            base.OnBackPressed();
        }
    }
}