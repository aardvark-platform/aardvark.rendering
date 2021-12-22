namespace Aardvark.Application

open FSharp.Data.Adaptive
open Aardvark.Base


type GamepadButton =
    | A = 0
    | B = 1
    | X = 2
    | Y = 3
    | LeftStick = 4
    | RightStick = 5
    | LeftShoulder = 6
    | RightShoulder = 7
    | Select = 8
    | Start = 9
    | CrossUp = 12
    | CrossDown = 13
    | CrossLeft = 14
    | CrossRight = 15
    | Unknown = 65535

type IGamepad =
    abstract Down : Aardvark.Base.IEvent<GamepadButton>
    abstract Up : Aardvark.Base.IEvent<GamepadButton>
    
    abstract A : aval<bool>
    abstract B : aval<bool>
    abstract X : aval<bool>
    abstract Y : aval<bool>
    abstract LeftStickDown : aval<bool>
    abstract RightStickDown : aval<bool>
    abstract LeftShoulder : aval<bool>
    abstract RightShoulder : aval<bool>
    abstract Select : aval<bool>
    abstract Start : aval<bool>
    abstract CrossUp : aval<bool>
    abstract CrossDown : aval<bool>
    abstract CrossLeft : aval<bool>
    abstract CrossRight : aval<bool>


    abstract LeftStick : aval<V2d>
    abstract RightStick : aval<V2d>
    abstract LeftTrigger : aval<float>
    abstract RightTrigger : aval<float>
