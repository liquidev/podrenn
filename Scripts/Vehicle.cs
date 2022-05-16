using Godot;
using System;

public class Vehicle : Spatial
{
   private bool tailLightsEnabled = false;

   [Export]
   public bool TailLightsEnabled
   {
      get
      {
         return this.tailLightsEnabled;
      }
      set
      {
         this.tailLightsEnabled = value;
         EmitSignal("UpdateTailLights");
      }
   }

   private TurnSignalMode turnSignal = TurnSignalMode.Off;

   [Export(PropertyHint.Enum, "Off,Left,Right")]
   public TurnSignalMode TurnSignal
   {
      get
      {
         return this.turnSignal;
      }
      set
      {
         this.turnSignal = value;
         EmitSignal("UpdateTurnSignals");
      }
   }

   [Signal]
   delegate void UpdateTailLights();

   [Signal]
   delegate void UpdateTurnSignals();

   public enum TurnSignalMode
   {
      Off,
      Left,
      Right,
   }
}
