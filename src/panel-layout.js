// Keep the entire panel within the current viewport, including after DPI/resize.
export function fitFloatingPanel({x,y,width}, viewport) {
  const vw=Number.isFinite(viewport.width)?Math.max(1,viewport.width):1;
  const vh=Number.isFinite(viewport.height)?Math.max(1,viewport.height):1;
  const w=Math.min(vw,Math.max(240,Number.isFinite(width)?width:320));
  const top=Math.min(60,Math.max(0,vh-180));
  const boundedY=Math.max(top,Math.min(Math.max(top,vh-180),Number.isFinite(y)?y:top));
  return {width:w,x:Math.max(0,Math.min(vw-w,Number.isFinite(x)?x:0)),
    y:boundedY,
    maxHeight:Math.max(1,vh-boundedY)};
}
