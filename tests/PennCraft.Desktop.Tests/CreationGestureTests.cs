using System.Windows;
using PennCraft.Desktop.Windows;
using Xunit;

namespace PennCraft.Desktop.Tests;

public sealed class CreationGestureTests
{
    [Theory]
    [InlineData(500, 400, 100, 100)]
    [InlineData(100, 400, 500, 100)]
    [InlineData(-500, -400, -100, -100)]
    public void RectangleSupportsAnyDirectionAndNegativeCoordinates(double x1, double y1, double x2, double y2)
    {
        var rectangle = CreationGesturePolicy.Rectangle(new Point(x1, y1), new Point(x2, y2));
        Assert.Equal(Math.Min(x1, x2), rectangle.Left);
        Assert.Equal(Math.Min(y1, y2), rectangle.Top);
        Assert.Equal(Math.Abs(x2 - x1), rectangle.Width);
        Assert.Equal(Math.Abs(y2 - y1), rectangle.Height);
    }

    [Theory]
    [InlineData(true, false, 1, true)]
    [InlineData(false, false, 1, false)]
    [InlineData(true, true, 1, false)]
    [InlineData(true, false, 2, false)]
    public void CreationNeedsKnownEmptyDesktopAndEnoughPhysicalSpace(bool known, bool selected, double scale, bool expected)
    {
        Assert.Equal(expected, CreationGesturePolicy.CanCreate(new Rect(10, 10, 300, 160),
            new Point(10, 10), scale, known, selected, [], []));
    }

    [Fact]
    public void IconsApplicationsAndExistingPensSuppressCreation()
    {
        var region = new Rect(100, 100, 500, 300);
        var start = region.TopLeft;
        Assert.False(CreationGesturePolicy.CanCreate(region, start, 1, true, false,
            [new Rect(110, 110, 50, 50)], []));
        Assert.False(CreationGesturePolicy.CanCreate(region, start, 1, true, false,
            [], [new Rect(590, 150, 200, 200)]));
        Assert.False(CreationGesturePolicy.CanCreate(region, start, 1, true, false,
            [], [new Rect(200, 200, 240, 120)]));
        Assert.True(CreationGesturePolicy.CanCreate(region, start, 1, true, false,
            [new Rect(800, 800, 50, 50)], [new Rect(-500, -500, 200, 200)]));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    [InlineData(239, 120)]
    [InlineData(240, 119)]
    public void AccidentalAndUndersizedDragsDoNotCreatePens(double width, double height)
    {
        Assert.False(CreationGesturePolicy.CanCreate(new Rect(0, 0, width, height),
            new Point(0, 0), 1, true, false, [], []));
    }

    [Fact]
    public void IconLayoutMustStayStableAcrossTheDrag()
    {
        var first = new DesktopIconSnapshot([new Rect(0, 0, 50, 50)], false, Environment.TickCount64);
        Assert.True(first.IsFresh);
        Assert.True(first.SameLayout(first with { HasSelection = true }));
        Assert.False(first.SameLayout(first with { Icons = [new Rect(100, 0, 50, 50)] }));
        Assert.False((first with { CapturedAt = Environment.TickCount64 - 2000 }).IsFresh);
    }

    [Fact]
    public void CreationPreviewOffersOnlyPenCreationAndAVisibleDisabledPortal()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var preview = new CreationPreview(new Rect(10, 10, 400, 200), _ => throw new InvalidOperationException());
                Assert.True(preview.CreateItem.IsEnabled);
                Assert.False(preview.PortalItem.IsEnabled);
                Assert.Equal(Visibility.Visible, preview.PortalItem.Visibility);
                preview.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
