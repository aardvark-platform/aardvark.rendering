namespace Aardvark.Application.WinForms

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices
open Aardvark.Base
open Aardvark.Application
open Aardvark.Application.WinForms
open System.Windows.Forms
open Aardvark.Rendering.Vulkan
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

[<AutoOpen>]
module private ``XCB Interop`` =
    type XcbWindow(connection : nativeint, root : uint32, id : uint32, closeAtom : uint32) =
        let renderEv = new Event<_>()

        let mouseDown = new Event<MouseEventHandler, MouseEventArgs>()
        let mouseUp = new Event<MouseEventHandler, MouseEventArgs>()
        let mouseScroll = new Event<MouseEventHandler, MouseEventArgs>()
        let mouseMove = new Event<MouseEventHandler, MouseEventArgs>()
        let mouseEnter = new Event<System.EventHandler, System.EventArgs>()
        let mouseLeave = new Event<System.EventHandler, System.EventArgs>()

        let keyDown = new Event<KeyEventHandler, KeyEventArgs>()
        let keyUp = new Event<KeyEventHandler, KeyEventArgs>()
        let keyDownWithRepeats = new Event<KeyEventHandler, KeyEventArgs>()

        let upTimes = System.Collections.Generic.Dictionary<Keys, int>()
        let downKeys = System.Collections.Generic.HashSet<Keys>()

        member internal x.Connection = connection
        member internal x.Id = id
        member internal x.CloseAtom = closeAtom
        member internal x.Root = root

        [<CLIEvent>]
        member x.MouseDown = mouseDown.Publish

        [<CLIEvent>]
        member x.MouseUp = mouseUp.Publish

        [<CLIEvent>]
        member x.MouseWheel = mouseScroll.Publish

        [<CLIEvent>]
        member x.MouseMove = mouseMove.Publish

        [<CLIEvent>]
        member x.MouseEnter = mouseEnter.Publish

        [<CLIEvent>]
        member x.MouseLeave = mouseLeave.Publish



        [<CLIEvent>]
        member x.KeyDown = keyDown.Publish

        [<CLIEvent>]
        member x.KeyDownWithRepeats = keyDownWithRepeats.Publish


        [<CLIEvent>]
        member x.KeyUp = keyUp.Publish



        [<CLIEvent>]
        member x.OnRender = renderEv.Publish

        member internal x.Render() =
            renderEv.Trigger(x, null)

        member internal __.RaiseMouseDown(x : int, y : int, button : MouseButtons) =
            mouseDown.Trigger(null, MouseEventArgs(button, 1, x, y, 0))

        member internal __.RaiseMouseUp(x : int, y : int, button : MouseButtons) =
            mouseUp.Trigger(null, MouseEventArgs(button, 1, x, y, 0))

        member internal __.RaiseMouseScroll(x : int, y : int, delta : int) =
            mouseScroll.Trigger(null, MouseEventArgs(MouseButtons.None, 0, x, y, delta))

        member internal __.RaiseMove(x : int, y : int) =
            mouseMove.Trigger(null, MouseEventArgs(MouseButtons.None, 0, x, y, 0))

        member internal __.RaiseEnter(x : int, y : int) =
            mouseEnter.Trigger(null, MouseEventArgs(MouseButtons.None, 0, x, y, 0))

        member internal __.RaiseLeave(x : int, y : int) =
            mouseLeave.Trigger(null, MouseEventArgs(MouseButtons.None, 0, x, y, 0))


        member internal __.RaiseKeyDown(keys : Keys) =
            if downKeys.Add keys then
                keyDown.Trigger(null, KeyEventArgs(keys))

            keyDownWithRepeats.Trigger(null, KeyEventArgs(keys))

        member internal __.RaiseKeyUp(keys : Keys) =
            if downKeys.Remove keys then
                keyUp.Trigger(null, KeyEventArgs(keys))

        

        member internal __.Idle() = ()


    module Xcb =

        type xcb_keys =
            | Escape = 0x9uy
            | F1 = 0x43uy
            | F2 = 0x44uy
            | F3 = 0x45uy
            | F4 = 0x46uy
            | F5 = 0x47uy
            | F6 = 0x48uy
            | F7 = 0x49uy
            | F8 = 0x4Auy
            | F9 = 0x4Buy
            | F10 = 0x4Cuy
            | F11 = 0x5Fuy
            | F12 = 0x60uy
            | D1 = 0xAuy
            | D2 = 0xBuy
            | D3 = 0xCuy
            | D4 = 0xDuy
            | D5 = 0xEuy
            | D6 = 0xFuy
            | D7 = 0x10uy
            | D8 = 0x11uy
            | D9 = 0x12uy
            | D0 = 0x13uy
            | Backspace = 0x16uy
            | Q = 0x18uy
            | W = 0x19uy
            | E = 0x1Auy
            | R = 0x1Buy
            | T = 0x1Cuy
            | Z = 0x1Duy
            | U = 0x1Euy
            | I = 0x1Fuy
            | O = 0x20uy
            | P = 0x21uy
            | Ue = 0x22uy
            | Plus = 0x23uy
            | Enter = 0x24uy
            | A = 0x26uy
            | S = 0x27uy
            | D = 0x28uy
            | F = 0x29uy
            | G = 0x2Auy
            | H = 0x2Buy
            | J = 0x2Cuy
            | K = 0x2Duy
            | L = 0x2Euy
            | Oe = 0x2Fuy
            | Ae = 0x30uy
            | Y = 0x34uy
            | X = 0x35uy
            | C = 0x36uy
            | V = 0x37uy
            | B = 0x38uy
            | N = 0x39uy
            | M = 0x3Auy
            | Comma = 0x3Buy
            | Dot = 0x3Cuy
            | Minus = 0x3Duy
            | Tab = 0x17uy
            | Sharp = 0x33uy
            | Smaller = 0x5Euy
            | CapsLock = 0x42uy
            | RightShift = 0x3Euy
            | ArrowUp = 0x6Fuy
            | ArrowDown = 0x74uy
            | ArrowLeft = 0x71uy
            | ArrowRight = 0x72uy
            | End = 0x73uy
            | PageDown = 0x75uy
            | PageUp = 0x70uy
            | Pos1 = 0x6Euy
            | Insert = 0x76uy
            | Delete = 0x77uy
            | Ctrl = 0x25uy
            | Windows = 0x85uy
            | Alt = 0x40uy
            | Space = 0x41uy
            | RightAlt = 0x6Cuy
            | RightCtrl = 0x69uy
            | Pause = 0x7Fuy
            | Circumflex = 0x31uy
            | Shift = 0x32uy
            | OpenBrackets = 0x14uy
            | Oem6 = 0x15uy

        let private lookupTable (l : list<'a * 'b>) =
            let d = System.Collections.Generic.Dictionary()
            for (k,v) in l do

                match d.TryGetValue k with
                    | (true, vo) -> failwithf "duplicated lookup-entry: %A (%A vs %A)" k vo v
                    | _ -> ()

                d.[k] <- v
            d

        let winFormsKeys =
            lookupTable [
                xcb_keys.A, Keys.A
                xcb_keys.B, Keys.B
                xcb_keys.C, Keys.C
                xcb_keys.D, Keys.D
                xcb_keys.E, Keys.E
                xcb_keys.F, Keys.F
                xcb_keys.G, Keys.G
                xcb_keys.H, Keys.H
                xcb_keys.I, Keys.I
                xcb_keys.J, Keys.J
                xcb_keys.K, Keys.K
                xcb_keys.L, Keys.L
                xcb_keys.M, Keys.M
                xcb_keys.N, Keys.N
                xcb_keys.O, Keys.O
                xcb_keys.P, Keys.P
                xcb_keys.Q, Keys.Q
                xcb_keys.R, Keys.R
                xcb_keys.S, Keys.S
                xcb_keys.T, Keys.T
                xcb_keys.U, Keys.U
                xcb_keys.V, Keys.V
                xcb_keys.W, Keys.W
                xcb_keys.X, Keys.X
                xcb_keys.Y, Keys.Y
                xcb_keys.Z, Keys.Z
                xcb_keys.Oe, Keys.Oemtilde
                xcb_keys.Ae, Keys.Oem7
                xcb_keys.Ue, Keys.Oem1

                xcb_keys.D0, Keys.D0
                xcb_keys.D1, Keys.D1
                xcb_keys.D2, Keys.D2
                xcb_keys.D3, Keys.D3
                xcb_keys.D4, Keys.D4
                xcb_keys.D5, Keys.D5
                xcb_keys.D6, Keys.D6
                xcb_keys.D7, Keys.D7
                xcb_keys.D8, Keys.D8
                xcb_keys.D9, Keys.D9

            
                xcb_keys.F1, Keys.F1
                xcb_keys.F2, Keys.F2
                xcb_keys.F3, Keys.F3
                xcb_keys.F4, Keys.F4
                xcb_keys.F5, Keys.F5
                xcb_keys.F6, Keys.F6
                xcb_keys.F7, Keys.F7
                xcb_keys.F8, Keys.F8
                xcb_keys.F9, Keys.F9
                xcb_keys.F10, Keys.F10
                xcb_keys.F11, Keys.F11
                xcb_keys.F12, Keys.F12


            
                xcb_keys.Alt, Keys.Menu
                xcb_keys.ArrowDown, Keys.Down
                xcb_keys.ArrowLeft, Keys.Left
                xcb_keys.ArrowRight, Keys.Right
                xcb_keys.ArrowUp, Keys.Up
                xcb_keys.Backspace, Keys.Back
                xcb_keys.Circumflex, Keys.Oem5
                xcb_keys.Comma, Keys.Oemcomma
                xcb_keys.Dot, Keys.OemPeriod
                xcb_keys.Ctrl, Keys.ControlKey
                xcb_keys.Delete, Keys.Delete
                xcb_keys.End, Keys.End
                xcb_keys.Pos1, Keys.Home
                xcb_keys.Enter, Keys.Return
                xcb_keys.Escape, Keys.Escape
                xcb_keys.Insert, Keys.Insert
                xcb_keys.Minus, Keys.OemMinus
                xcb_keys.PageDown, Keys.PageDown
                xcb_keys.PageUp, Keys.PageUp
                xcb_keys.Pause, Keys.Pause
                xcb_keys.Plus, Keys.Oemplus
                xcb_keys.RightAlt, Keys.RMenu
                xcb_keys.RightCtrl, Keys.RControlKey
                xcb_keys.RightShift, Keys.RShiftKey
                xcb_keys.Sharp, Keys.OemQuestion
                xcb_keys.Shift, Keys.ShiftKey
                xcb_keys.Smaller, Keys.OemBackslash
                xcb_keys.Space, Keys.Space
                xcb_keys.Tab, Keys.Tab
                xcb_keys.Windows, Keys.LWin
                xcb_keys.OpenBrackets, Keys.OemOpenBrackets
                xcb_keys.Oem6, Keys.Oem6
            ]

        let toWinFormsKey (k : xcb_keys) =
            match winFormsKeys.TryGetValue k with
                | (true, k) -> k
                | _ -> Keys.None

        type xcb_connection = nativeint
        type xcb_window = uint32
        type xcb_colormap = uint32
        type xcb_visualid = uint32
        type xcb_atom = uint32

        // NOT SURE
        type xcb_intern_atom_cookie = uint32

        type xcb_intern_atom_reply =
            struct
               val mutable public response_type  : uint8    
               val mutable public pad0           : uint8    
               val mutable public sequence       : uint16   
               val mutable public length         : uint32   
               val mutable public atom           : xcb_atom 
            end

        module xcb_atom_enum =
            let XCB_ATOM_NONE = 0u
            let XCB_ATOM_ANY = 0u
            let XCB_ATOM_PRIMARY = 1u
            let XCB_ATOM_SECONDARY = 2u
            let XCB_ATOM_ARC = 3u
            let XCB_ATOM_ATOM = 4u
            let XCB_ATOM_BITMAP = 5u
            let XCB_ATOM_CARDINAL = 6u
            let XCB_ATOM_COLORMAP = 7u
            let XCB_ATOM_CURSOR = 8u
            let XCB_ATOM_CUT_BUFFER0 = 9u
            let XCB_ATOM_CUT_BUFFER1 = 10u
            let XCB_ATOM_CUT_BUFFER2 = 11u
            let XCB_ATOM_CUT_BUFFER3 = 12u
            let XCB_ATOM_CUT_BUFFER4 = 13u
            let XCB_ATOM_CUT_BUFFER5 = 14u
            let XCB_ATOM_CUT_BUFFER6 = 15u
            let XCB_ATOM_CUT_BUFFER7 = 16u
            let XCB_ATOM_DRAWABLE = 17u
            let XCB_ATOM_FONT = 18u
            let XCB_ATOM_INTEGER = 19u
            let XCB_ATOM_PIXMAP = 20u
            let XCB_ATOM_POINT = 21u
            let XCB_ATOM_RECTANGLE = 22u
            let XCB_ATOM_RESOURCE_MANAGER = 23u
            let XCB_ATOM_RGB_COLOR_MAP = 24u
            let XCB_ATOM_RGB_BEST_MAP = 25u
            let XCB_ATOM_RGB_BLUE_MAP = 26u
            let XCB_ATOM_RGB_DEFAULT_MAP = 27u
            let XCB_ATOM_RGB_GRAY_MAP = 28u
            let XCB_ATOM_RGB_GREEN_MAP = 29u
            let XCB_ATOM_RGB_RED_MAP = 30u
            let XCB_ATOM_STRING = 31u
            let XCB_ATOM_VISUALID = 32u
            let XCB_ATOM_WINDOW = 33u
            let XCB_ATOM_WM_COMMAND = 34u
            let XCB_ATOM_WM_HINTS = 35u
            let XCB_ATOM_WM_CLIENT_MACHINE = 36u
            let XCB_ATOM_WM_ICON_NAME = 37u
            let XCB_ATOM_WM_ICON_SIZE = 38u
            let XCB_ATOM_WM_NAME = 39u
            let XCB_ATOM_WM_NORMAL_HINTS = 40u
            let XCB_ATOM_WM_SIZE_HINTS = 41u
            let XCB_ATOM_WM_ZOOM_HINTS = 42u
            let XCB_ATOM_MIN_SPACE = 43u
            let XCB_ATOM_NORM_SPACE = 44u
            let XCB_ATOM_MAX_SPACE = 45u
            let XCB_ATOM_END_SPACE = 46u
            let XCB_ATOM_SUPERSCRIPT_X = 47u
            let XCB_ATOM_SUPERSCRIPT_Y = 48u
            let XCB_ATOM_SUBSCRIPT_X = 49u
            let XCB_ATOM_SUBSCRIPT_Y = 50u
            let XCB_ATOM_UNDERLINE_POSITION = 51u
            let XCB_ATOM_UNDERLINE_THICKNESS = 52u
            let XCB_ATOM_STRIKEOUT_ASCENT = 53u
            let XCB_ATOM_STRIKEOUT_DESCENT = 54u
            let XCB_ATOM_ITALIC_ANGLE = 55u
            let XCB_ATOM_X_HEIGHT = 56u
            let XCB_ATOM_QUAD_WIDTH = 57u
            let XCB_ATOM_WEIGHT = 58u
            let XCB_ATOM_POINT_SIZE = 59u
            let XCB_ATOM_RESOLUTION = 60u
            let XCB_ATOM_COPYRIGHT = 61u
            let XCB_ATOM_NOTICE = 62u
            let XCB_ATOM_FONT_NAME = 63u
            let XCB_ATOM_FAMILY_NAME = 64u
            let XCB_ATOM_FULL_NAME = 65u
            let XCB_ATOM_CAP_HEIGHT = 66u
            let XCB_ATOM_WM_CLASS = 67u
            let XCB_ATOM_WM_TRANSIENT_FOR = 68u


        type xcb_window_class =
            | XCB_WINDOW_CLASS_COPY_FROM_PARENT = 0us
            | XCB_WINDOW_CLASS_INPUT_OUTPUT = 1us
            | XCB_WINDOW_CLASS_INPUT_ONLY = 2us

        type xcb_event_mask =
            | XCB_EVENT_MASK_NO_EVENT = 0u
            | XCB_EVENT_MASK_KEY_PRESS = 1u
            | XCB_EVENT_MASK_KEY_RELEASE = 2u
            | XCB_EVENT_MASK_BUTTON_PRESS = 4u
            | XCB_EVENT_MASK_BUTTON_RELEASE = 8u
            | XCB_EVENT_MASK_ENTER_WINDOW = 16u
            | XCB_EVENT_MASK_LEAVE_WINDOW = 32u
            | XCB_EVENT_MASK_POINTER_MOTION = 64u
            | XCB_EVENT_MASK_POINTER_MOTION_HINT = 128u
            | XCB_EVENT_MASK_BUTTON_1_MOTION = 256u
            | XCB_EVENT_MASK_BUTTON_2_MOTION = 512u
            | XCB_EVENT_MASK_BUTTON_3_MOTION = 1024u
            | XCB_EVENT_MASK_BUTTON_4_MOTION = 2048u
            | XCB_EVENT_MASK_BUTTON_5_MOTION = 4096u
            | XCB_EVENT_MASK_BUTTON_MOTION = 8192u
            | XCB_EVENT_MASK_KEYMAP_STATE = 16384u
            | XCB_EVENT_MASK_EXPOSURE = 32768u
            | XCB_EVENT_MASK_VISIBILITY_CHANGE = 65536u
            | XCB_EVENT_MASK_STRUCTURE_NOTIFY = 131072u
            | XCB_EVENT_MASK_RESIZE_REDIRECT = 262144u
            | XCB_EVENT_MASK_SUBSTRUCTURE_NOTIFY = 524288u
            | XCB_EVENT_MASK_SUBSTRUCTURE_REDIRECT = 1048576u
            | XCB_EVENT_MASK_FOCUS_CHANGE = 2097152u
            | XCB_EVENT_MASK_PROPERTY_CHANGE = 4194304u
            | XCB_EVENT_MASK_COLOR_MAP_CHANGE = 8388608u
            | XCB_EVENT_MASK_OWNER_GRAB_BUTTON = 16777216u

        type xcb_all =
            | XCB_KEY_PRESS = 2uy
            | XCB_KEY_RELEASE = 3uy
            | XCB_BUTTON_PRESS = 4uy
            | XCB_BUTTON_RELEASE = 5uy
            | XCB_MOTION_NOTIFY = 6uy
            | XCB_ENTER_NOTIFY = 7uy
            | XCB_LEAVE_NOTIFY = 8uy
            | XCB_FOCUS_IN = 9uy
            | XCB_FOCUS_OUT = 10uy
            | XCB_KEYMAP_NOTIFY = 11uy
            | XCB_EXPOSE = 12uy
            | XCB_GRAPHICS_EXPOSURE = 13uy
            | XCB_NO_EXPOSURE = 14uy
            | XCB_VISIBILITY_NOTIFY = 15uy
            | XCB_CREATE_NOTIFY = 16uy
            | XCB_DESTROY_NOTIFY = 17uy
            | XCB_UNMAP_NOTIFY = 18uy
            | XCB_MAP_NOTIFY = 19uy
            | XCB_MAP_REQUEST = 20uy
            | XCB_REPARENT_NOTIFY = 21uy
            | XCB_CONFIGURE_NOTIFY = 22uy
            | XCB_CONFIGURE_REQUEST = 23uy
            | XCB_GRAVITY_NOTIFY = 24uy
            | XCB_RESIZE_REQUEST = 25uy
            | XCB_CIRCULATE_NOTIFY = 26uy
            | XCB_CIRCULATE_REQUEST = 27uy
            | XCB_PROPERTY_NOTIFY = 28uy
            | XCB_SELECTION_CLEAR = 29uy
            | XCB_SELECTION_REQUEST = 30uy
            | XCB_SELECTION_NOTIFY = 31uy
            | XCB_COLORMAP_NOTIFY = 32uy
            | XCB_CLIENT_MESSAGE = 33uy
            | XCB_MAPPING_NOTIFY = 34uy
            | XCB_GE_GENERIC = 35uy
            | XCB_REQUEST = 1uy
            | XCB_VALUE = 2uy
            | XCB_WINDOW = 3uy
            | XCB_PIXMAP = 4uy
            | XCB_ATOM = 5uy
            | XCB_CURSOR = 6uy
            | XCB_FONT = 7uy
            | XCB_MATCH = 8uy
            | XCB_DRAWABLE = 9uy
            | XCB_ACCESS = 10uy
            | XCB_ALLOC = 11uy
            | XCB_COLORMAP = 12uy
            | XCB_G_CONTEXT = 13uy
            | XCB_ID_CHOICE = 14uy
            | XCB_NAME = 15uy
            | XCB_LENGTH = 16uy
            | XCB_IMPLEMENTATION = 17uy
            | XCB_CREATE_WINDOW = 1uy
            | XCB_CHANGE_WINDOW_ATTRIBUTES = 2uy
            | XCB_GET_WINDOW_ATTRIBUTES = 3uy
            | XCB_DESTROY_WINDOW = 4uy
            | XCB_DESTROY_SUBWINDOWS = 5uy
            | XCB_CHANGE_SAVE_SET = 6uy
            | XCB_REPARENT_WINDOW = 7uy
            | XCB_MAP_WINDOW = 8uy
            | XCB_MAP_SUBWINDOWS = 9uy
            | XCB_UNMAP_WINDOW = 10uy
            | XCB_UNMAP_SUBWINDOWS = 11uy
            | XCB_CONFIGURE_WINDOW = 12uy
            | XCB_CIRCULATE_WINDOW = 13uy
            | XCB_GET_GEOMETRY = 14uy
            | XCB_QUERY_TREE = 15uy
            | XCB_INTERN_ATOM = 16uy
            | XCB_GET_ATOM_NAME = 17uy
            | XCB_CHANGE_PROPERTY = 18uy
            | XCB_DELETE_PROPERTY = 19uy
            | XCB_GET_PROPERTY = 20uy
            | XCB_LIST_PROPERTIES = 21uy
            | XCB_SET_SELECTION_OWNER = 22uy
            | XCB_GET_SELECTION_OWNER = 23uy
            | XCB_CONVERT_SELECTION = 24uy
            | XCB_SEND_EVENT = 25uy
            | XCB_GRAB_POINTER = 26uy
            | XCB_UNGRAB_POINTER = 27uy
            | XCB_GRAB_BUTTON = 28uy
            | XCB_UNGRAB_BUTTON = 29uy
            | XCB_CHANGE_ACTIVE_POINTER_GRAB = 30uy
            | XCB_GRAB_KEYBOARD = 31uy
            | XCB_UNGRAB_KEYBOARD = 32uy
            | XCB_GRAB_KEY = 33uy
            | XCB_UNGRAB_KEY = 34uy
            | XCB_ALLOW_EVENTS = 35uy
            | XCB_GRAB_SERVER = 36uy
            | XCB_UNGRAB_SERVER = 37uy
            | XCB_QUERY_POINTER = 38uy
            | XCB_GET_MOTION_EVENTS = 39uy
            | XCB_TRANSLATE_COORDINATES = 40uy
            | XCB_WARP_POINTER = 41uy
            | XCB_SET_INPUT_FOCUS = 42uy
            | XCB_GET_INPUT_FOCUS = 43uy
            | XCB_QUERY_KEYMAP = 44uy
            | XCB_OPEN_FONT = 45uy
            | XCB_CLOSE_FONT = 46uy
            | XCB_QUERY_FONT = 47uy
            | XCB_QUERY_TEXT_EXTENTS = 48uy
            | XCB_LIST_FONTS = 49uy
            | XCB_LIST_FONTS_WITH_INFO = 50uy
            | XCB_SET_FONT_PATH = 51uy
            | XCB_GET_FONT_PATH = 52uy
            | XCB_CREATE_PIXMAP = 53uy
            | XCB_FREE_PIXMAP = 54uy
            | XCB_CREATE_GC = 55uy
            | XCB_CHANGE_GC = 56uy
            | XCB_COPY_GC = 57uy
            | XCB_SET_DASHES = 58uy
            | XCB_SET_CLIP_RECTANGLES = 59uy
            | XCB_FREE_GC = 60uy
            | XCB_CLEAR_AREA = 61uy
            | XCB_COPY_AREA = 62uy
            | XCB_COPY_PLANE = 63uy
            | XCB_POLY_POINT = 64uy
            | XCB_POLY_LINE = 65uy
            | XCB_POLY_SEGMENT = 66uy
            | XCB_POLY_RECTANGLE = 67uy
            | XCB_POLY_ARC = 68uy
            | XCB_FILL_POLY = 69uy
            | XCB_POLY_FILL_RECTANGLE = 70uy
            | XCB_POLY_FILL_ARC = 71uy
            | XCB_PUT_IMAGE = 72uy
            | XCB_GET_IMAGE = 73uy
            | XCB_POLY_TEXT_8 = 74uy
            | XCB_POLY_TEXT_16 = 75uy
            | XCB_IMAGE_TEXT_8 = 76uy
            | XCB_IMAGE_TEXT_16 = 77uy
            | XCB_CREATE_COLORMAP = 78uy
            | XCB_FREE_COLORMAP = 79uy
            | XCB_COPY_COLORMAP_AND_FREE = 80uy
            | XCB_INSTALL_COLORMAP = 81uy
            | XCB_UNINSTALL_COLORMAP = 82uy
            | XCB_LIST_INSTALLED_COLORMAPS = 83uy
            | XCB_ALLOC_COLOR = 84uy
            | XCB_ALLOC_NAMED_COLOR = 85uy
            | XCB_ALLOC_COLOR_CELLS = 86uy
            | XCB_ALLOC_COLOR_PLANES = 87uy
            | XCB_FREE_COLORS = 88uy
            | XCB_STORE_COLORS = 89uy
            | XCB_STORE_NAMED_COLOR = 90uy
            | XCB_QUERY_COLORS = 91uy
            | XCB_LOOKUP_COLOR = 92uy
            | XCB_CREATE_CURSOR = 93uy
            | XCB_CREATE_GLYPH_CURSOR = 94uy
            | XCB_FREE_CURSOR = 95uy
            | XCB_RECOLOR_CURSOR = 96uy
            | XCB_QUERY_BEST_SIZE = 97uy
            | XCB_QUERY_EXTENSION = 98uy
            | XCB_LIST_EXTENSIONS = 99uy
            | XCB_CHANGE_KEYBOARD_MAPPING = 100uy
            | XCB_GET_KEYBOARD_MAPPING = 101uy
            | XCB_CHANGE_KEYBOARD_CONTROL = 102uy
            | XCB_GET_KEYBOARD_CONTROL = 103uy
            | XCB_BELL = 104uy
            | XCB_CHANGE_POINTER_CONTROL = 105uy
            | XCB_GET_POINTER_CONTROL = 106uy
            | XCB_SET_SCREEN_SAVER = 107uy
            | XCB_GET_SCREEN_SAVER = 108uy
            | XCB_CHANGE_HOSTS = 109uy
            | XCB_LIST_HOSTS = 110uy
            | XCB_SET_ACCESS_CONTROL = 111uy
            | XCB_SET_CLOSE_DOWN_MODE = 112uy
            | XCB_KILL_CLIENT = 113uy
            | XCB_ROTATE_PROPERTIES = 114uy
            | XCB_FORCE_SCREEN_SAVER = 115uy
            | XCB_SET_POINTER_MAPPING = 116uy
            | XCB_GET_POINTER_MAPPING = 117uy
            | XCB_SET_MODIFIER_MAPPING = 118uy
            | XCB_GET_MODIFIER_MAPPING = 119uy
            | XCB_NO_OPERATION = 127uy

        type xcb_prop_mode =
            | XCB_PROP_MODE_REPLACE = 0uy
            | XCB_PROP_MODE_PREPEND = 1uy
            | XCB_PROP_MODE_APPEND = 2uy


        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_screen =
            struct
                val mutable public root                  :  xcb_window  
                val mutable public default_colormap      :  xcb_colormap 
                val mutable public white_pixel           :  uint32       
                val mutable public black_pixel           :  uint32       
                val mutable public current_input_masks   :  uint32       
                val mutable public width_in_pixels       :  uint16       
                val mutable public height_in_pixels      :  uint16       
                val mutable public width_in_millimeters  :  uint16       
                val mutable public height_in_millimeters :  uint16       
                val mutable public min_installed_maps    :  uint16       
                val mutable public max_installed_maps    :  uint16       
                val mutable public root_visual           :  xcb_visualid 
                val mutable public backing_stores        :  uint8        
                val mutable public save_unders           :  uint8        
                val mutable public root_depth            :  uint8        
                val mutable public allowed_depths_len    :  uint8        
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_screen_iterator =
            struct
                 val mutable data : nativeptr<xcb_screen> 
                 val mutable rem : int
                 val mutable index : int
            end


        [<StructLayout(LayoutKind.Explicit, Size = 28)>]
        type uint7 = struct end
        type xcb_generic_event =
            struct
                val mutable public response_type  : uint8  
                val mutable public pad0           : uint8  
                val mutable public sequence       : uint16 
                val mutable public pad            : uint7 
                val mutable public full_sequence  : uint32 
            end


        [<StructLayout(LayoutKind.Explicit, Size = 20)>]
        type xcb_client_message_data =
            struct

                member x.ReadUInt8 (index : int) =
                    if index >= 20 then raise <| IndexOutOfRangeException()

                    let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<uint8>
                    NativePtr.get ptr index

                member x.ReadUInt16 (index : int) =
                    if index >= 10 then raise <| IndexOutOfRangeException()

                    let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<uint16>
                    NativePtr.get ptr index

                member x.ReadUInt32 (index : int) =
                    if index >= 5 then raise <| IndexOutOfRangeException()

                    let ptr = &&x |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<uint32>
                    NativePtr.get ptr index
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_client_message_event =
            struct
                val mutable public response_type : uint8                  
                val mutable public format        : uint8                 
                val mutable public sequence      : uint16                  
                val mutable public window        : xcb_window             
                val mutable public mtype         : xcb_atom                
                val mutable public data          : xcb_client_message_data 
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_key_press_event =
            struct
                val mutable public response_type : uint8      
                val mutable public detail        : xcb_keys
                val mutable public sequence      : uint16      
                val mutable public time          : uint32 
                val mutable public root          : xcb_window    
                val mutable public event         : xcb_window    
                val mutable public child         : xcb_window    
                val mutable public root_x        : int16         
                val mutable public root_y        : int16         
                val mutable public event_x       : int16         
                val mutable public event_y       : int16         
                val mutable public state         : uint16        
                val mutable public same_screen   : uint8         
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_button_press_event =
            struct
                val mutable public response_type : uint8         
                val mutable public detail        : uint8    
                val mutable public sequence      : uint16        
                val mutable public time          : uint32 
                val mutable public root          : xcb_window    
                val mutable public event         : xcb_window    
                val mutable public child         : xcb_window    
                val mutable public root_x        : int16         
                val mutable public root_y        : int16         
                val mutable public event_x       : int16         
                val mutable public event_y       : int16         
                val mutable public state         : uint16        
                val mutable public same_screen   : uint8         
                val mutable public pad0          : uint8         
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_motion_notify_event =
            struct
                val mutable public response_type : uint8         
                val mutable public detail        : uint8         
                val mutable public sequence      : uint16        
                val mutable public time          : uint32 
                val mutable public root          : xcb_window    
                val mutable public event         : xcb_window    
                val mutable public child         : xcb_window    
                val mutable public root_x        : int16         
                val mutable public root_y        : int16         
                val mutable public event_x       : int16         
                val mutable public event_y       : int16         
                val mutable public state         : uint16        
                val mutable public same_screen   : uint8         
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_configure_notify_event = 
            struct
                val mutable public response_type      : uint8      
                val mutable public pad0               : uint8      
                val mutable public sequence           : uint16     
                val mutable public event              : xcb_window
                val mutable public window             : xcb_window
                val mutable public above_sibling      : xcb_window 
                val mutable public x                  : int16    
                val mutable public y                  : int16     
                val mutable public width              : uint16   
                val mutable public height             : uint16   
                val mutable public border_width       : uint16    
                val mutable public override_redirect  : uint8     
                val mutable public pad1               : uint8     
            end

        [<StructLayout(LayoutKind.Sequential)>]
        type xcb_enter_notify_event =
            struct
                val mutable public response_type        : uint8         
                val mutable public detail               : uint8    
                val mutable public sequence             : uint16        
                val mutable public time                 : uint32 
                val mutable public root                 : xcb_window    
                val mutable public event                : xcb_window    
                val mutable public child                : xcb_window    
                val mutable public root_x               : int16         
                val mutable public root_y               : int16         
                val mutable public event_x              : int16         
                val mutable public event_y              : int16         
                val mutable public state                : uint16        
                val mutable public mode                 : uint8         
                val mutable public same_screen_focus    : uint8         
            end



        [<DllImport("X11-xcb")>]
        extern xcb_connection XGetXCBConnection(nativeint xdisplay)



        [<DllImport("xcb")>]
        extern xcb_connection xcb_connect(string displayName, int* screenNum)

        [<DllImport("xcb")>]
        extern void xcb_disconnect(xcb_connection connection)

        [<DllImport("xcb")>]
        extern nativeint xcb_get_setup(nativeint connection)

        [<DllImport("xcb")>]
        extern xcb_screen_iterator xcb_setup_roots_iterator(nativeint setup)

        [<DllImport("xcb")>]
        extern void xcb_screen_next (xcb_screen_iterator* it)

        [<DllImport("xcb")>]
        extern xcb_window xcb_generate_id(xcb_connection connection)

        [<DllImport("xcb")>]
        extern nativeint xcb_create_window(
            xcb_connection conn, 
            uint8 depth,
            xcb_window wid,
            xcb_window parent, 
            int16 x, 
            int16 y, 
            uint16 width,
            uint16 height, 
        
            uint16 border_width, 
            xcb_window_class _class, 
            xcb_visualid visual,
            uint32 value_mask, 
            uint32 *value_list)

        [<DllImport("xcb")>]
        extern void xcb_map_window(xcb_connection connection, xcb_window window)

        [<DllImport("xcb")>]
        extern void xcb_flush(xcb_connection connection)

        [<DllImport("xcb")>]
        extern xcb_generic_event* xcb_wait_for_event(xcb_connection connection)

        [<DllImport("xcb")>]
        extern xcb_generic_event* xcb_poll_for_event(xcb_connection connection)

        [<DllImport("xcb")>]
        extern xcb_generic_event* xcb_poll_for_queued_event(xcb_connection connection)

        [<DllImport("libc", EntryPoint = "free")>]
        extern void private free_ (nativeint ptr)

        let free (ptr : nativeptr<'a>) = free_ (NativePtr.toNativeInt ptr)

        [<DllImport("libc", EntryPoint = "free")>]
        extern void xcb_free_event (xcb_generic_event* connection)

        [<DllImport("xcb")>]
        extern xcb_intern_atom_cookie xcb_intern_atom(xcb_connection conn, uint8 only_if_exists, uint16 name_len, string name)

        [<DllImport("xcb")>]
        extern xcb_intern_atom_reply* xcb_intern_atom_reply(xcb_connection conn, xcb_intern_atom_cookie cookie, nativeint err)

        [<DllImport("xcb")>]
        extern void xcb_change_property(xcb_connection conn, xcb_prop_mode mode, xcb_window window, xcb_atom property, xcb_atom _type, uint8 format, uint32 data_len, nativeint data)

        let mutable private connection = 0n
        let mutable private screen = xcb_screen()

        let connect() =
            if connection = 0n then
                let mutable screenNo = 0
                connection <- xcb_connect(null, &&screenNo)

                if connection = 0n then
                    failwith "could not find compatible driver"

                let setup = xcb_get_setup(connection)
                let mutable it = xcb_setup_roots_iterator(setup)
                for i in 0..screenNo-1 do
                    xcb_screen_next(&&it)

                screen <- NativePtr.read it.data

    //            printfn "Informations of screen %A:" screen.root
    //            printfn "  width.........: %A" screen.width_in_pixels
    //            printfn "  height........: %A" screen.height_in_pixels
    //            printfn "  white pixel...: %A" screen.white_pixel
    //            printfn "  black pixel...: %A" screen.black_pixel

            connection

        let disconnect() =
            if connection <> 0n then
                xcb_disconnect(connection)
                connection <- 0n
                screen <- xcb_screen()

        let createWindow (name : string) (width : int) (height : int) =
            let c = connect()
            let id = xcb_generate_id(c)

            let values = NativePtr.stackalloc 32


            let eventMask =
                xcb_event_mask.XCB_EVENT_MASK_EXPOSURE |||
                xcb_event_mask.XCB_EVENT_MASK_STRUCTURE_NOTIFY |||

                xcb_event_mask.XCB_EVENT_MASK_POINTER_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_PRESS |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_RELEASE |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_1_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_2_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_3_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_4_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_BUTTON_5_MOTION |||
                xcb_event_mask.XCB_EVENT_MASK_LEAVE_WINDOW |||
                xcb_event_mask.XCB_EVENT_MASK_ENTER_WINDOW |||

                xcb_event_mask.XCB_EVENT_MASK_KEY_RELEASE |||
                xcb_event_mask.XCB_EVENT_MASK_KEY_PRESS |||
                xcb_event_mask.XCB_EVENT_MASK_KEYMAP_STATE


            NativePtr.set values 0 screen.black_pixel
            NativePtr.set values 1 (uint32 eventMask)
            NativePtr.set values 2 0u
            let w =
                xcb_create_window(
                    c,
                    0uy, //XCB_COPY_FROM_PARENT
                    id,
                    screen.root,
                    0s,
                    0s,
                    uint16 width,
                    uint16 height,
                    0us,
                    xcb_window_class.XCB_WINDOW_CLASS_INPUT_OUTPUT,
                    screen.root_visual,
                    2u ||| 2048u, // XCB_CW_BACK_PIXEL ||| XCB_CW_EVENT_MASK
                    values
                )


            let cookie = xcb_intern_atom(c, 1uy, 12us, "WM_PROTOCOLS")
            let replyP = xcb_intern_atom_reply(c, cookie, 0n)
            let reply = replyP |> NativePtr.read

            let cookie2 = xcb_intern_atom(c, 0uy, 16us, "WM_DELETE_WINDOW")
            let wm_delete_window = xcb_intern_atom_reply(c, cookie2, 0n) |> NativePtr.read

            let data = Marshal.AllocHGlobal(sizeof<xcb_atom>) |> NativePtr.ofNativeInt
            NativePtr.write data wm_delete_window.atom

            xcb_change_property(c, xcb_prop_mode.XCB_PROP_MODE_REPLACE, id, reply.atom, 4u, 32uy, 1u, NativePtr.toNativeInt data)

            let ptr = CStr.salloc name //Marshal.StringToHGlobalAnsi(name)
            let length = name.Length
            xcb_change_property(c, xcb_prop_mode.XCB_PROP_MODE_REPLACE, id, xcb_atom_enum.XCB_ATOM_WM_NAME, xcb_atom_enum.XCB_ATOM_STRING, 8uy, uint32 length, NativePtr.toNativeInt ptr)

            free(replyP)
            xcb_map_window(c, id)
        
        
            XcbWindow(c, screen.root, id, wm_delete_window.atom)

        type MouseButton =
            | Left = 1uy
            | Middle = 2uy
            | Right = 3uy
            | ScrollUp = 4uy
            | ScrollDown = 5uy

        let toMouseButton (b : uint8) =
            match b with
                | 1uy -> System.Windows.Forms.MouseButtons.Left
                | 2uy -> System.Windows.Forms.MouseButtons.Middle
                | 3uy -> System.Windows.Forms.MouseButtons.Right
                | _ -> System.Windows.Forms.MouseButtons.None

        let private isRepeatUp(e : xcb_key_press_event) (pnext : nativeptr<xcb_generic_event>) =
            if pnext |> NativePtr.toNativeInt <> 0n then
                let n = NativePtr.read pnext
                let eventType = n.response_type &&& 0x7fuy |> unbox<xcb_all>

                match eventType with
                    | xcb_all.XCB_KEY_PRESS -> 
                        let ptr = pnext |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_key_press_event>
                        let ne = NativePtr.read ptr
                        if ne.time = e.time && e.detail = ne.detail then true
                        else false
                    | e ->
                        false

            else
                false


        let handleEvent (w : XcbWindow, c : xcb_connection, pe : nativeptr<xcb_generic_event>, pnext : nativeptr<xcb_generic_event>) =
            let e = NativePtr.read pe
            let eventType = e.response_type &&& 0x7fuy |> unbox<xcb_all>


            match eventType with
                | xcb_all.XCB_EXPOSE ->
                    ()
                    //w.Render()

                | xcb_all.XCB_CLIENT_MESSAGE ->
                    let ptr = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_client_message_event>
                    let e = NativePtr.read ptr
                    let atom = e.data.ReadUInt32(0)
                
                    if atom = w.CloseAtom then
                        raise <| OperationCanceledException()

                | xcb_all.XCB_DESTROY_NOTIFY ->
                    raise <| OperationCanceledException()

                | xcb_all.XCB_CONFIGURE_NOTIFY -> ()
    //                let ptr = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_configure_notify_event>
    //                let e = NativePtr.read ptr

                    //printfn "size: [%d, %d]" e.width e.height
                
                | xcb_all.XCB_KEY_PRESS ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_key_press_event> |> NativePtr.read
                    let k = toWinFormsKey e.detail
                    if k <> Keys.None then
                        w.RaiseKeyDown(k)

                | xcb_all.XCB_KEY_RELEASE ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_key_press_event> |> NativePtr.read
                    let k = toWinFormsKey e.detail

                    if isRepeatUp e pnext then
                        w.RaiseKeyDown(k)
                    else
                        //printfn "release: { detail = %A; state = %A; response_type = %A; same_screen = %A; time = %A }" e.detail e.state e.response_type e.same_screen e.time
                        if k <> Keys.None then
                            w.RaiseKeyUp(k)

                | xcb_all.XCB_BUTTON_PRESS ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_button_press_event> |> NativePtr.read

                    match e.detail with
                        | 4uy -> w.RaiseMouseScroll(int e.event_x, int e.event_y, 120)
                        | 5uy -> w.RaiseMouseScroll(int e.event_x, int e.event_y, -120)
                        | _ ->
                            let b = toMouseButton e.detail
                            w.RaiseMouseDown(int e.event_x, int e.event_y, b)
     
                | xcb_all.XCB_BUTTON_RELEASE ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_button_press_event> |> NativePtr.read

                    match e.detail with
                        | 4uy | 5uy -> ()
                        | _ ->
                            let b = toMouseButton e.detail
                            w.RaiseMouseUp(int e.event_x, int e.event_y, b)
            
                | xcb_all.XCB_MOTION_NOTIFY ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_motion_notify_event> |> NativePtr.read

                    w.RaiseMove(int e.event_x, int e.event_y)

                | xcb_all.XCB_ENTER_NOTIFY ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_enter_notify_event> |> NativePtr.read

                    w.RaiseEnter(int e.event_x, int e.event_y)

                | xcb_all.XCB_LEAVE_NOTIFY ->
                    let e = pe |> NativePtr.toNativeInt |> NativePtr.ofNativeInt<xcb_enter_notify_event> |> NativePtr.read

                    w.RaiseLeave(int e.event_x, int e.event_y)

                | _ -> 
                    Aardvark.Base.Log.line "event %A" eventType



        let run (w : XcbWindow) =
            let c = connect()

            let sw = System.Diagnostics.Stopwatch()
            let mutable iterations = 0
            try
                xcb_flush(c)
                try
                    sw.Start()
                    while true do
                        let e = xcb_poll_for_event(c)
                        let n = xcb_poll_for_queued_event(c)

                        if e |> NativePtr.toNativeInt <> 0n then
                            try handleEvent (w, c, e, n)
                            finally xcb_free_event(e)
                        else
                            w.Idle()
                            w.Render()
                            iterations <- iterations + 1

                        if sw.Elapsed.TotalMilliseconds > 2000.0 then
                            sw.Stop()
                            let fps = float iterations / sw.Elapsed.TotalSeconds
                            Aardvark.Base.Log.line "fps: %A" fps
                            sw.Restart()
                            iterations <- 0

                with :? OperationCanceledException ->
                    //printfn "closing"
                    ()

            finally disconnect()

type VulkanGameWindowXcb(r : Runtime) =
    
    let window = Xcb.createWindow "GameWindow" 1024 768



    member x.Run() =
        Xcb.run window
