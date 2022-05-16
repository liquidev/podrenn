using Godot;
using System;

public class NotSoFast : Vehicle
{
   private Spatial tailLights;
   private Spatial leftTurnSignals;
   private Spatial rightTurnSignals;
   private Timer turnSignalBlinkTimer;

   [Export(PropertyHint.Range, "0,16")]
   public float tailLightEnergy = 2.0f;

   [Export(PropertyHint.Range, "0,16")]
   public float turnSignalEnergy = 0.75f;

   private bool turnSignalOn = true;

   public override void _Ready()
   {
      tailLights = (Spatial)GetNode("TailLights");
      leftTurnSignals = (Spatial)GetNode("TurnSignals/Left");
      rightTurnSignals = (Spatial)GetNode("TurnSignals/Right");
      turnSignalBlinkTimer = (Timer)GetNode("TurnSignals/BlinkTimer");
   }

   public void _SetTailLights()
   {
      foreach (Light light in tailLights.GetChildren())
      {
         light.LightEnergy = TailLightsEnabled ? tailLightEnergy : 0.0f;
      }
   }

   public void _SetTurnSignals()
   {
      if (TurnSignal != TurnSignalMode.Off)
      {
         turnSignalOn = true;
         _BlinkTurnSignals();
         turnSignalBlinkTimer.Start();
      }
      else
      {
         turnSignalBlinkTimer.Stop();
         foreach (Light light in leftTurnSignals.GetChildren())
         {
            light.LightEnergy = 0.0f;
         }
         foreach (Light light in rightTurnSignals.GetChildren())
         {
            light.LightEnergy = 0.0f;
         }
      }
   }

   public void _BlinkTurnSignals()
   {
      var lights = TurnSignal == TurnSignalMode.Left ? leftTurnSignals : rightTurnSignals;
      foreach (Light light in lights.GetChildren())
      {
         light.LightEnergy = turnSignalOn ? turnSignalEnergy : 0.0f;
      }
      turnSignalOn = !turnSignalOn;
   }
}
