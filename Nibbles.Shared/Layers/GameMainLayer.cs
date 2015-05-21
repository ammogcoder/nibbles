﻿using System;
using System.Collections.Generic;
using CocosSharp;
using Nibbles.Shared.Nodes;
using Nibbles.Shared.Helpers;
using System.Linq;
using CocosDenshion;

namespace Nibbles.Shared.Layers
{
	public class GameMainLayer : CCLayerGradient
	{
		CCDrawNode line;
		List<Bubble> visibleBubbles;
		List<Bubble> burstedBubbles;
		List<Bubble> frozenBubbles;
		CCPoint lastPoint;

		float elapsedTime;
		int redColorIncrement = 10;
		int redColorIncrementEnd = 20;
		Bubble hitBubble;
		CCLabel scoreLabel, multiplierLabel, countdown;
		const int baseFont = 48;
		Int64 currentScore;

		const float GAME_DURATION = 63.5f; // game ends after 63.5 seconds

		public GameMainLayer () : base (CCColor4B.Blue, new CCColor4B(127, 200, 205))
		{
			// Load and instantate your assets here
			visibleBubbles = new List<Bubble> ();
			burstedBubbles = new List<Bubble> ();
			frozenBubbles = new List<Bubble> ();
			// Make any renderable node objects (e.g. sprites) children of this layer
			Color = new CCColor3B(127, 200, 205);
			Opacity = 200;
			line = new CCDrawNode ();
			line.ZOrder = int.MaxValue;
			StartScheduling ();	
		}

		protected override void AddedToScene ()
		{
			base.AddedToScene ();

			// Use the bounds to layout the positioning of our drawable assets
			CCRect bounds = VisibleBoundsWorldspace;

			//Add line that we will use to draw later on
			AddChild (line, 1);

			// Register for touch events
			var touchListener = new CCEventListenerTouchAllAtOnce ();
			touchListener.OnTouchesEnded = OnTouchesEnded;
			touchListener.OnTouchesMoved = OnTouchesMoved;
			touchListener.OnTouchesBegan = OnTouchesBegan;
			AddEventListener (touchListener, this);

			//setup
			scoreLabel = new CCLabel("0", GameAppDelegate.MainFont, 48, CCLabelFormat.SystemFont) {
				Position = new CCPoint(bounds.Size.Width / 2, bounds.Size.Height - 60),
				Color = CCColor3B.White,
				HorizontalAlignment = CCTextAlignment.Center,
				VerticalAlignment = CCVerticalTextAlignment.Top,
				AnchorPoint = CCPoint.AnchorMiddle,
			};

			multiplierLabel = new CCLabel(string.Empty, GameAppDelegate.MainFont, 48, CCLabelFormat.SystemFont) {
				Position = new CCPoint(bounds.Size.Width - 60, 60),
				Color = CCColor3B.White,
				HorizontalAlignment = CCTextAlignment.Right,
				VerticalAlignment = CCVerticalTextAlignment.Center,
				AnchorPoint = CCPoint.AnchorMiddle
			};

			countdown = new CCLabel("60", GameAppDelegate.MainFont, 36, CCLabelFormat.SystemFont) {
				Position = new CCPoint(120, 60),
				Color = CCColor3B.White,
				HorizontalAlignment = CCTextAlignment.Right,
				VerticalAlignment = CCVerticalTextAlignment.Center,
				AnchorPoint = CCPoint.AnchorMiddle
			};

			AddChild (scoreLabel, 1);
			AddChild (multiplierLabel, 1);
			AddChild (countdown, 1);

			//add initial bubbles
			ScheduleOnce (t => visibleBubbles.Add (AddBubble ()), .25f);
			ScheduleOnce (t => visibleBubbles.Add (AddBubble ()), .25f);
			ScheduleOnce (t => visibleBubbles.Add (AddBubble ()), .25f);
			ScheduleOnce (t => visibleBubbles.Add (AddBubble ()), .25f);
		}

		void OnTouchesEnded (List<CCTouch> touches, CCEvent touchEvent)
		{
			if (touches.Count <= 0)
				return;
			if (hitBubble == null)
				return;

			TallyScore ();
		}

		void OnTouchesBegan (List<CCTouch> touches, CCEvent touchEvent)
		{
			if (touches.Count <= 0)
				return;

			var touch = touches [0];

			var bubbles = (from bubble in visibleBubbles
				where bubble.ContainsPoint(touch.Location)
				orderby bubble.Id
				select bubble).ToList();

			if (bubbles.Count == 0)
				return;

			hitBubble = bubbles [0];
			if (hitBubble == null)
				return;

			hitBubble.Freeze (0);
		
			frozenBubbles.Add (hitBubble);

			lastPoint = touch.Location;
			line.Clear ();
			multiplierLabel.Text = string.Empty;
		}


		void OnTouchesMoved (List<CCTouch> touches, CCEvent touchEvent)
		{
			if (touches.Count <= 0)
				return;

			if (hitBubble == null)
				return;

			var touch = touches [0];

			line.DrawSegment (lastPoint, touch.Location, 3, hitBubble.ColorF);
			lastPoint = touch.Location;

			var hitOtherColor = (from bubble in visibleBubbles
				where bubble.ContainsPoint(touch.Location) &&
				bubble.ColorId !=  hitBubble.ColorId
				select true).Any();

			if (hitOtherColor) {
				foreach (var bubble in frozenBubbles) {
					bubble.ForcePop (this);
					visibleBubbles.Remove (bubble);
				}

				line.Clear ();
				frozenBubbles.Clear ();
				hitBubble = null;
				multiplierLabel.Text = string.Empty;
				return;
			}

			var bubbles = (from bubble in visibleBubbles
					where bubble.ContainsPoint(touch.Location) &&
				bubble.ColorId == hitBubble.ColorId &&
				!bubble.IsFrozen
				select bubble).ToList();

			if (bubbles == null || !bubbles.Any ())
				return;

			foreach (var bubble in bubbles) {
				frozenBubbles.Add (bubble);
				bubble.Freeze (frozenBubbles.Count);
			}

			if (frozenBubbles.Count > 1) {
				multiplierLabel.SystemFontSize = baseFont + (frozenBubbles.Count * 2);
				multiplierLabel.Text = (frozenBubbles.Count - 1) + "x";
			}

			if (frozenBubbles.Count >= 6) {
				TallyScore ();
				CCSimpleAudioEngine.SharedEngine.PlayEffect("sounds/highscore");

			}
		}

		void TallyScore(){
			int score = 0;
			int multiplier = frozenBubbles.Count - 1;
			foreach (var bubble in frozenBubbles) {
				score += bubble.Points;
				bubble.ForcePop (this);
				visibleBubbles.Remove (bubble);
			}

			score *= multiplier;

			if (multiplier < 0)
				score = 0;
			else if (multiplier == 0)//  1 bubble
				score = 20;
		
			line.Clear ();
			hitBubble = null;
			frozenBubbles.Clear ();
			UpdateScore (score);
			multiplierLabel.Text = string.Empty;
		}

		void StartScheduling(){

	

			Schedule (t => {
				if (ShouldEndGame ()) {
					EndGame ();
					return;
				}
				visibleBubbles.Add (AddBubble ());

				if(CCRandom.Next(0, 100) > 90)
					visibleBubbles.Add (AddBubble ());

				var left = (GAME_DURATION - elapsedTime);
				if (left < 10 && CCRandom.Next(0, 100) > 30)
					visibleBubbles.Add (AddBubble ());
			}, .5f);

			Schedule (t => CheckPop ());

			// Schedule for method to be called every 0.1s
			Schedule (UpdateLayerGradient, 0.1f);
		}

		void EndGame ()
		{
			// Stop scheduled events as we transition to game over scene
			UnscheduleAll();

			var gameOverScene = GameOverLayer.CreateScene (Window, currentScore);
			var transitionToGameOver = new CCTransitionMoveInR (0.3f, gameOverScene);

			Director.ReplaceScene (transitionToGameOver);
		}

		bool ShouldEndGame ()
		{
			return elapsedTime > GAME_DURATION;
		}

		Bubble AddBubble(){
		
			var bubble = new Bubble ();
			var p = Utils.GetRandomPosition (bubble.ContentSize, VisibleBoundsWorldspace.Size);
			bubble.Position = p;

			AddChild (bubble);

			return bubble;
		}

		void CheckPop(){


			foreach (var bubble in visibleBubbles) {
				if (!bubble.Pop (this))
					continue;

				burstedBubbles.Add (bubble);
			}

			foreach (var bubble in burstedBubbles)
			{
				visibleBubbles.Remove(bubble);
				UpdateScore (-10);
			}

			burstedBubbles.Clear ();
		}
			

		private void UpdateScore(Int64 toAdd){

			currentScore += toAdd;
			scoreLabel.Text = currentScore.ToString ();

			if (currentScore > Settings.HighScore)
				scoreLabel.Color = CCColor3B.Yellow;
			else if (currentScore < 0)
				scoreLabel.Color = CCColor3B.Red;
			else
				scoreLabel.Color = CCColor3B.White;
		}
	

		public static CCScene CreateScene (CCWindow mainWindow)
		{
			var scene = new CCScene (mainWindow);
			var layer = new GameMainLayer ();

			scene.AddChild (layer);

			return scene;
		}

		protected void UpdateLayerGradient (float dt)
		{
			elapsedTime += dt;
			var left = (GAME_DURATION - elapsedTime);
			if (left < 0)
				left = 0;

			countdown.Text = left.ToString ("0#.0");
			CCColor3B startColor = this.StartColor;

			var increment = redColorIncrement;
			if (left < 10)
				increment = redColorIncrementEnd;

			int newRedColor = startColor.R + increment;

			if (newRedColor <= byte.MinValue) {
				newRedColor = 0;
				redColorIncrement *= -1;
				redColorIncrementEnd *= -1;
			} else if (newRedColor >= byte.MaxValue) {
				newRedColor = byte.MaxValue;
				redColorIncrement *= -1;
				redColorIncrementEnd *= -1;
			}

			startColor.R = (byte)(newRedColor);

			StartColor = startColor;
		}
	}
}
