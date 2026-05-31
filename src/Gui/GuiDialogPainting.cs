using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VSpaint
{
    public class GuiDialogPainting : GuiDialog
    {
        private const int CanvasW      = PaintingUtil.Width;
        private const int CanvasH      = PaintingUtil.Height;
        private const int DisplayScale = 4;
        private const int DisplayW     = CanvasW * DisplayScale;
        private const int DisplayH     = CanvasH * DisplayScale;

        private const int SwatchSize = 20;
        private const int SwatchGap  = 3;
        private const int MaxUndo    = 10;

        private readonly BlockPos easelPos;

        // Palette indices with a corresponding wet brush in the hotbar; snapshotted
        // at GUI-open time so painting can only use what was available then.
        private readonly HashSet<int> availableColors;

        private int[] pixels = new int[PaintingUtil.PixelCount];

        private int  selectedColor = 1;
        private int  brushRadius   = 0;   // 0=1px, 1=3px, 2=5px
        private bool eraserMode    = false;
        private bool isDirty       = false;
        private bool finishConfirmMode = false;

        private bool isDrawing = false;
        private int  lastDrawX = -1;
        private int  lastDrawY = -1;
        private readonly Queue<ValueTuple<int,int>> smoothHistory = new Queue<ValueTuple<int,int>>();

        private readonly List<int[]> undoStack = new List<int[]>();

        private double paletteOriginY;

        public GuiDialogPainting(ICoreClientAPI capi, BlockPos easelPos, byte[] existingPixelData, HashSet<int> availableColors)
            : base(capi)
        {
            this.easelPos       = easelPos;
            this.availableColors = availableColors ?? new HashSet<int>();

            if (existingPixelData != null && existingPixelData.Length >= PaintingUtil.EncodedSize)
                pixels = PaintingUtil.DecodePixels(existingPixelData);

            // Prefer the active brush's color so the player keeps painting in the
            // color they had selected; fall back to the first available otherwise.
            int activeColor = ItemPaintbrush.GetColor(capi.World.Player?.InventoryManager?.ActiveHotbarSlot?.Itemstack);
            if (activeColor > 0 && this.availableColors.Contains(activeColor))
                selectedColor = activeColor;
            else
                foreach (int c in this.availableColors)
                    if (c > 0 && c < 16) { selectedColor = c; break; }
        }

        public override string ToggleKeyCombinationCode => null;
        public override bool   PrefersUngrabbedMouse    => true;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            SetupDialog();
            capi.World.Player.Entity.AnimManager.StartAnimation(new Vintagestory.API.Common.AnimationMetaData
            {
                Animation     = "petanimal",
                Code          = "vspaint-painting",
                AnimationSpeed = 1.2f,
                EaseInSpeed   = 10f,
                EaseOutSpeed  = 10f,
                Weight        = 1f
            }.Init());
        }

        private void SetupDialog()
        {
            double canvasY    = 30;
            double paletteY   = canvasY + DisplayH + 10;
            double brushRowY  = paletteY + SwatchSize + 10;
            double buttonRowY = brushRowY + 30 + 8;

            double paletteRowW = 16 * (SwatchSize + SwatchGap) - SwatchGap;
            paletteOriginY = paletteY;

            var dlgBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
            var bgBounds  = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            var canvasBounds  = ElementBounds.Fixed(0, canvasY,  DisplayW,    DisplayH);
            var paletteBounds = ElementBounds.Fixed(0, paletteY, paletteRowW, SwatchSize);

            var brushLblBounds = ElementBounds.Fixed(0,   brushRowY, 60, 25);
            var brush1Bounds   = ElementBounds.Fixed(65,  brushRowY, 40, 25);
            var brush3Bounds   = ElementBounds.Fixed(110, brushRowY, 40, 25);
            var brush5Bounds   = ElementBounds.Fixed(155, brushRowY, 40, 25);
            var eraserBounds   = ElementBounds.Fixed(205, brushRowY, 60, 25);

            var composer = capi.Gui.CreateCompo("vspaint-painting", dlgBounds)
                .AddShadedDialogBG(bgBounds, true, 5.0, 0.75f)
                .AddDialogTitleBar(Lang.Get("vspaint:dialog-title"), OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddDynamicCustomDraw(canvasBounds, OnDrawCanvas, "canvas")
                .AddDynamicCustomDraw(paletteBounds, OnDrawPalette, "palette")
                .AddStaticText(Lang.Get("vspaint:brush-size"), CairoFont.WhiteSmallText(), brushLblBounds)
                .AddToggleButton("1",                       CairoFont.WhiteSmallText(), _ => SetBrushRadius(0), brush1Bounds, "brush1")
                .AddToggleButton("3",                       CairoFont.WhiteSmallText(), _ => SetBrushRadius(1), brush3Bounds, "brush3")
                .AddToggleButton("5",                       CairoFont.WhiteSmallText(), _ => SetBrushRadius(2), brush5Bounds, "brush5")
                .AddToggleButton(Lang.Get("vspaint:eraser"), CairoFont.WhiteSmallText(), _ => ToggleEraser(),   eraserBounds, "eraser");

            if (finishConfirmMode)
            {
                var warnBounds   = ElementBounds.Fixed(0,   buttonRowY,      paletteRowW, 25);
                var yesBounds    = ElementBounds.Fixed(0,   buttonRowY + 32, 120,         28);
                var cancelBounds = ElementBounds.Fixed(130, buttonRowY + 32, 80,          28);

                composer
                    .AddStaticText(Lang.Get("vspaint:finish-warning"), CairoFont.WhiteSmallText(), warnBounds)
                    .AddSmallButton(Lang.Get("vspaint:finish-confirm"), OnFinishConfirm, yesBounds,    EnumButtonStyle.Normal, "finishyes")
                    .AddSmallButton(Lang.Get("vspaint:cancel"),         OnFinishCancel,  cancelBounds, EnumButtonStyle.Normal, "finishcancel");
            }
            else
            {
                double bw = 58;
                var undoBounds   = ElementBounds.Fixed(0,          buttonRowY, bw, 28);
                var clearBounds  = ElementBounds.Fixed(bw+5,       buttonRowY, bw, 28);
                var saveBounds   = ElementBounds.Fixed((bw+5)*2,   buttonRowY, bw, 28);
                var finishBounds = ElementBounds.Fixed((bw+5)*3,   buttonRowY, bw, 28);
                var closeBounds  = ElementBounds.Fixed((bw+5)*4,   buttonRowY, bw, 28);

                composer
                    .AddSmallButton(Lang.Get("vspaint:undo"),   OnUndo,         undoBounds,   EnumButtonStyle.Normal, "undo")
                    .AddSmallButton(Lang.Get("vspaint:clear"),  OnClear,        clearBounds,  EnumButtonStyle.Normal, "clear")
                    .AddSmallButton(Lang.Get("vspaint:save"),   OnSave,         saveBounds,   EnumButtonStyle.Normal, "save")
                    .AddSmallButton(Lang.Get("vspaint:finish"), OnFinishRequest, finishBounds, EnumButtonStyle.Normal, "finish")
                    .AddSmallButton(Lang.Get("vspaint:close"),  OnClose,        closeBounds,  EnumButtonStyle.Normal, "close");
            }

            SingleComposer = composer.EndChildElements().Compose();

            UpdateBrushButtons();
        }

        private void RebuildDialog()
        {
            SetupDialog();
        }

        private void OnDrawCanvas(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            ctx.SetSourceRGBA(1, 1, 1, 1);
            ctx.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
            ctx.Fill();

            double scaleX = bounds.InnerWidth  / (double)CanvasW;
            double scaleY = bounds.InnerHeight / (double)CanvasH;

            using (var pixSurface = new ImageSurface(Format.ARGB32, CanvasW, CanvasH))
            {
                int stride   = pixSurface.Stride;
                var rawBytes = new byte[stride * CanvasH];

                for (int y = 0; y < CanvasH; y++)
                {
                    for (int x = 0; x < CanvasW; x++)
                    {
                        int colorIdx = pixels[y * CanvasW + x] & 0xF;
                        int argb     = PaintingUtil.Palette[colorIdx];
                        int off      = y * stride + x * 4;
                        rawBytes[off]     = (byte)( argb        & 0xFF);
                        rawBytes[off + 1] = (byte)((argb >>  8) & 0xFF);
                        rawBytes[off + 2] = (byte)((argb >> 16) & 0xFF);
                        rawBytes[off + 3] = 0xFF;
                    }
                }

                Marshal.Copy(rawBytes, 0, pixSurface.DataPtr, rawBytes.Length);
                pixSurface.MarkDirty();

                ctx.Save();
                ctx.Scale(scaleX, scaleY);
                ctx.SetSourceSurface(pixSurface, 0, 0);
                if (ctx.GetSource() is SurfacePattern sp) sp.Filter = Filter.Fast;
                ctx.Paint();
                ctx.Restore();
            }

            ctx.SetSourceRGBA(0.25, 0.25, 0.25, 1);
            ctx.LineWidth = 1.5;
            ctx.Rectangle(0, 0, bounds.InnerWidth, bounds.InnerHeight);
            ctx.Stroke();
        }

        private void OnDrawPalette(Context ctx, ImageSurface surface, ElementBounds bounds)
        {
            for (int i = 0; i < 16; i++)
            {
                int argb = PaintingUtil.Palette[i];
                double r = ((argb >> 16) & 0xFF) / 255.0;
                double g = ((argb >>  8) & 0xFF) / 255.0;
                double b = ( argb        & 0xFF) / 255.0;

                double sx = i * (SwatchSize + SwatchGap);
                bool available = availableColors.Contains(i);
                bool selected  = (i == selectedColor && !eraserMode);

                // Dim unavailable colors so it's obvious which are usable.
                double alpha = available ? 1.0 : 0.25;
                ctx.SetSourceRGBA(r, g, b, alpha);
                ctx.Rectangle(sx, 0, SwatchSize, SwatchSize);
                ctx.Fill();

                if (selected)
                    ctx.SetSourceRGB(1, 1, 1);
                else if (available)
                    ctx.SetSourceRGB(0.3, 0.3, 0.3);
                else
                    ctx.SetSourceRGBA(0.3, 0.3, 0.3, 0.3);

                ctx.LineWidth = selected ? 2.5 : 1;
                ctx.Rectangle(sx, 0, SwatchSize, SwatchSize);
                ctx.Stroke();
            }
        }

        public override void OnMouseDown(MouseEvent args)
        {
            base.OnMouseDown(args);

            if (TryPickPaletteColor(args.X, args.Y))
            {
                args.Handled = true;
                return;
            }

            ElementBounds cb = SingleComposer.GetCustomDraw("canvas").Bounds;
            if (!cb.PointInside(args.X, args.Y)) return;

            PushUndoSnapshot();
            smoothHistory.Clear();
            isDrawing = true;
            lastDrawX = -1;
            lastDrawY = -1;

            var (cx, cy) = ScreenToCanvas(args.X, args.Y, cb);
            DrawAt(cx, cy);
        }

        public override void OnMouseUp(MouseEvent args)
        {
            base.OnMouseUp(args);
            isDrawing = false;
            lastDrawX = -1;
            lastDrawY = -1;
            smoothHistory.Clear();
        }

        public override void OnMouseMove(MouseEvent args)
        {
            base.OnMouseMove(args);
            if (!isDrawing) return;

            ElementBounds cb = SingleComposer.GetCustomDraw("canvas").Bounds;
            if (!cb.PointInside(args.X, args.Y)) return;

            var (cx, cy) = ScreenToCanvas(args.X, args.Y, cb);
            var (sx, sy) = Smooth(cx, cy);

            if (lastDrawX >= 0)
                DrawLine(lastDrawX, lastDrawY, sx, sy);
            else
                DrawAt(sx, sy);

            lastDrawX = sx;
            lastDrawY = sy;
        }

        private (int, int) ScreenToCanvas(double screenX, double screenY, ElementBounds cb)
        {
            int cx = (int)((screenX - cb.absX) / cb.InnerWidth  * CanvasW);
            int cy = (int)((screenY - cb.absY) / cb.InnerHeight * CanvasH);
            return (cx, cy);
        }

        private (int, int) Smooth(int rawX, int rawY)
        {
            smoothHistory.Enqueue((rawX, rawY));
            if (smoothHistory.Count > 3) smoothHistory.Dequeue();
            int sx = 0, sy = 0;
            foreach (var (x, y) in smoothHistory) { sx += x; sy += y; }
            return (sx / smoothHistory.Count, sy / smoothHistory.Count);
        }

        private void DrawLine(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1,  sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                DrawBrushStamp(x0, y0);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 <  dx) { err += dx; y0 += sy; }
            }
            RedrawCanvas();
        }

        private void DrawAt(int x, int y)
        {
            DrawBrushStamp(x, y);
            RedrawCanvas();
        }

        private void DrawBrushStamp(int cx, int cy)
        {
            isDirty = true;
            int colorIdx = eraserMode ? 0 : selectedColor;

            for (int dy = -brushRadius; dy <= brushRadius; dy++)
            {
                for (int dx = -brushRadius; dx <= brushRadius; dx++)
                {
                    if (dx * dx + dy * dy > brushRadius * brushRadius) continue;
                    int px = cx + dx;
                    int py = cy + dy;
                    if (px < 0 || px >= CanvasW || py < 0 || py >= CanvasH) continue;
                    pixels[py * CanvasW + px] = colorIdx;
                }
            }
        }

        private void RedrawCanvas()
        {
            SingleComposer.GetCustomDraw("canvas")?.Redraw();
        }

        private bool TryPickPaletteColor(double screenX, double screenY)
        {
            var pb = SingleComposer.GetCustomDraw("palette")?.Bounds;
            if (pb == null || !pb.PointInside(screenX, screenY)) return false;

            double relX = screenX - pb.absX;
            int idx = (int)(relX / (SwatchSize + SwatchGap));
            if (idx < 0 || idx >= 16) return false;

            if (!availableColors.Contains(idx)) return false;

            selectedColor = idx;
            eraserMode    = false;
            SingleComposer.GetCustomDraw("palette").Redraw();
            UpdateBrushButtons();
            return true;
        }

        private void SetBrushRadius(int sizeOption)
        {
            brushRadius = sizeOption;
            eraserMode  = false;
            UpdateBrushButtons();
        }

        private void ToggleEraser()
        {
            eraserMode = !eraserMode;
            UpdateBrushButtons();
        }

        private void UpdateBrushButtons()
        {
            if (SingleComposer == null) return;

            SingleComposer.GetToggleButton("brush1")?.SetValue(!eraserMode && brushRadius == 0);
            SingleComposer.GetToggleButton("brush3")?.SetValue(!eraserMode && brushRadius == 1);
            SingleComposer.GetToggleButton("brush5")?.SetValue(!eraserMode && brushRadius == 2);
            SingleComposer.GetToggleButton("eraser")?.SetValue(eraserMode);
        }

        private bool OnUndo()
        {
            if (undoStack.Count == 0) return true;
            int last = undoStack.Count - 1;
            Array.Copy(undoStack[last], pixels, pixels.Length);
            undoStack.RemoveAt(last);
            isDirty = true;
            RedrawCanvas();
            return true;
        }

        private bool OnClear()
        {
            PushUndoSnapshot();
            Array.Clear(pixels, 0, pixels.Length);
            isDirty = true;
            RedrawCanvas();
            return true;
        }

        private bool OnSave()
        {
            SaveToServer();
            return true;
        }

        private bool OnFinishRequest()
        {
            finishConfirmMode = true;
            RebuildDialog();
            return true;
        }

        private bool OnFinishConfirm()
        {
            SaveToServer();
            capi.ModLoader.GetModSystem<VSpaintModSystem>()?.NetworkHandler?.SendFinish(easelPos);
            TryClose();
            return true;
        }

        private bool OnFinishCancel()
        {
            finishConfirmMode = false;
            RebuildDialog();
            return true;
        }

        private bool OnClose()
        {
            TryClose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            capi.World.Player.Entity.AnimManager.StopAnimation("vspaint-painting");
            if (isDirty)
                SaveToServer();
            undoStack.Clear();
            smoothHistory.Clear();
        }

        private void SaveToServer()
        {
            if (!isDirty) return;
            byte[] encoded = PaintingUtil.EncodePixels(pixels);
            capi.ModLoader.GetModSystem<VSpaintModSystem>()?.NetworkHandler?.SendSave(easelPos, encoded);
            isDirty = false;

            var player = capi.World?.Player;
            if (player != null)
            {
                ItemSlot brushSlot = player.InventoryManager.ActiveHotbarSlot;
                if (brushSlot?.Itemstack?.Collectible is ItemPaintbrush brush)
                    brush.UseBrush(brushSlot, player);
            }
        }

        private void PushUndoSnapshot()
        {
            var snap = new int[pixels.Length];
            Array.Copy(pixels, snap, pixels.Length);
            undoStack.Add(snap);
            if (undoStack.Count > MaxUndo)
                undoStack.RemoveAt(0);
        }
    }
}
