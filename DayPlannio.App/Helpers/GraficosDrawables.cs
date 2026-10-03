using DayPlannio.App.ViewModels;
using Microsoft.Maui.Graphics;

namespace DayPlannio.App.Helpers
{
    public class DonutChartDrawable : IDrawable
    {
        private readonly MetricasViewModel _vm;

        public DonutChartDrawable(MetricasViewModel vm)
        {
            _vm = vm;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var items = _vm.PorServico;
            if (items.Count == 0)
                return;

            float cx = dirtyRect.Center.X;
            float cy = dirtyRect.Center.Y;
            float rOuter = Math.Min(dirtyRect.Width, dirtyRect.Height) / 2f - 4;
            float rInner = rOuter * 0.62f;

            float total = (float)items.Sum(i => i.Percentual);
            bool vazio = total <= 0;

            float angulo = -90f;

            if (vazio)
            {
                canvas.FillColor = Color.FromArgb("#E6EAEA");
                canvas.FillCircle(cx, cy, rOuter);
                canvas.FillColor = Colors.White;
                canvas.FillCircle(cx, cy, rInner);
                return;
            }

            foreach (var item in items)
            {
                float sweep = (float)(item.Percentual / 100.0 * 360.0);
                if (sweep <= 0)
                    continue;

                canvas.FillColor = item.Cor;
                canvas.FillArc(
                    cx - rOuter,
                    cy - rOuter,
                    rOuter * 2,
                    rOuter * 2,
                    angulo,
                    angulo + sweep,
                    true);

                angulo += sweep;
            }

            canvas.FillColor = Colors.White;
            canvas.FillCircle(cx, cy, rInner);
        }
    }

    public class BarChartDrawable : IDrawable
    {
        private readonly MetricasViewModel _vm;

        public BarChartDrawable(MetricasViewModel vm)
        {
            _vm = vm;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var items = _vm.Evolucao;
            if (items.Count == 0)
                return;

            float max = items.Max(i => i.Quantidade);
            float maxV = max > 0 ? max : 1;

            float baseLine = dirtyRect.Height - 18;
            float topMargin = 6;

            float slot = dirtyRect.Width / items.Count;
            float barWidth = Math.Max(4, slot * 0.55f);

            var cor = Color.FromArgb("#4DB6AC");

            canvas.StrokeColor = Color.FromArgb("#E6EAEA");
            canvas.StrokeSize = 1;
            canvas.DrawLine(0, baseLine, dirtyRect.Width, baseLine);

            for (int i = 0; i < items.Count; i++)
            {
                float valor = items[i].Quantidade;
                float h = (valor / maxV) * (baseLine - topMargin);
                if (valor > 0 && h < 3)
                    h = 3;

                float x = slot * i + (slot - barWidth) / 2;
                float y = baseLine - h;

                canvas.FillColor = cor;
                canvas.FillRoundedRectangle(x, y, barWidth, h, Math.Min(4, barWidth / 2));
            }
        }
    }
}
