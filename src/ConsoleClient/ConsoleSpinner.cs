using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ConsoleClient
{
    internal class ConsoleSpinner
    {
        private int _currentAnimationFrame;
        private Timer tmr;

        public ConsoleSpinner()
        {
            SpinnerAnimationFrames = new[]
            {
                '|',
                '/',
                '-',
                '\\'
            };
            
        }

        private void statusChecker(object state)
        {
            throw new NotImplementedException();
        }

        public char[] SpinnerAnimationFrames { get; set; }

        public void UpdateProgress()
        {
            //Hide the cursor
            Console.CursorVisible = false;

            // Store the current position of the cursor
            var originalX = Console.CursorLeft;
            var originalY = Console.CursorTop;

            // Write the next frame (character) in the spinner animation
            Console.Write(SpinnerAnimationFrames[_currentAnimationFrame]);

            // Keep looping around all the animation frames
            _currentAnimationFrame++;
            if (_currentAnimationFrame == SpinnerAnimationFrames.Length)
            {
                _currentAnimationFrame = 0;
            }

            // Restore cursor to original position
            Console.SetCursorPosition(originalX, originalY);
        }
    }
}
