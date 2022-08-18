#include "pch.h"
#include "BoundsToolOverlayUI.h"

void DrawBoundsToolTick(BoundsToolState& toolState, HWND overlayWindow, D2DState& d2dState)
{
    if (!toolState.currentRegionStart.has_value())
    {
        return;
    }

    POINT cursorPos = {};
    GetCursorPos(&cursorPos);
    ScreenToClient(overlayWindow, &cursorPos);

    const D2D1_RECT_F rect{ .left = toolState.currentRegionStart->x,
                            .top = toolState.currentRegionStart->y,
                            .right = static_cast<float>(cursorPos.x),
                            .bottom = static_cast<float>(cursorPos.y) };
    d2dState.rt->DrawRectangle(rect, d2dState.solidBrushes[Brush::line].get());

    // TODO: fix
    wchar_t measureStringBuf[32] = {};
    const uint32_t textLen = swprintf_s(measureStringBuf,
                                        L"%.0f x %.0f",
                                        std::abs(rect.right - rect.left),
                                        std::abs(rect.top - rect.bottom));
    d2dState.DrawTextBox(measureStringBuf,
                         textLen,
                         toolState.currentRegionStart->x,
                         toolState.currentRegionStart->y,
                         overlayWindow);
}