using DayPlannio.Api.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DayPlannio.Api.Services
{
    public class RelatorioPdfService
    {
        public byte[] GerarRelatorioFinanceiro(
            string periodo,
            DateTime dataInicio,
            DateTime dataFim,
            decimal receitaAgendamentos,
            decimal custoAgendamentos,
            decimal entradasAvulsas,
            decimal saidasAvulsas,
            decimal lucroBruto,
            decimal lucroLiquido,
            int totalServicos,
            List<Agendamento> agendamentos,
            List<Financeiro> registros,
            List<Cliente> clientes,
            List<TipoServico> tiposServico)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item()
                            .Text("DayPlannio — Relatório Financeiro")
                            .FontSize(18)
                            .Bold()
                            .AlignCenter();

                        col.Item()
                            .Text($"Período: {periodo.ToUpper()} | {dataInicio:dd/MM/yyyy} até {dataFim:dd/MM/yyyy}")
                            .FontSize(10)
                            .AlignCenter();

                        col.Item()
                            .PaddingTop(5)
                            .LineHorizontal(1);
                    });

                    page.Content().Column(col =>
                    {
                        col.Item()
                            .PaddingTop(15)
                            .Text("Resumo")
                            .FontSize(14)
                            .Bold();

                        col.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Cell().Text("Receita dos Agendamentos");
                                table.Cell().Text($"R$ {receitaAgendamentos:F2}");

                                table.Cell().Text("Custo dos Agendamentos");
                                table.Cell().Text($"R$ {custoAgendamentos:F2}");

                                table.Cell().Text("Entradas Avulsas");
                                table.Cell().Text($"R$ {entradasAvulsas:F2}");

                                table.Cell().Text("Saídas Avulsas");
                                table.Cell().Text($"R$ {saidasAvulsas:F2}");

                                table.Cell().Text("Lucro Bruto").Bold();
                                table.Cell().Text($"R$ {lucroBruto:F2}").Bold();

                                table.Cell().Text("Lucro Líquido").Bold();
                                table.Cell().Text($"R$ {lucroLiquido:F2}").Bold();

                                table.Cell().Text("Total de Serviços");
                                table.Cell().Text($"{totalServicos}");
                            });

                        col.Item()
                            .PaddingTop(20)
                            .Text("Agendamentos Concluídos")
                            .FontSize(14)
                            .Bold();

                        col.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Data").Bold();
                                    header.Cell().Text("Cliente").Bold();
                                    header.Cell().Text("Serviço").Bold();
                                    header.Cell().Text("Valor Cobrado").Bold();
                                    header.Cell().Text("Custo Material").Bold();
                                });

                                foreach (var a in agendamentos)
                                {
                                    var cliente = clientes.FirstOrDefault(c => c.Id == a.ClienteId);
                                    var tipoServico = tiposServico.FirstOrDefault(t => t.Id == a.TipoServicoId);

                                    table.Cell().Text(a.DataHora.ToString("dd/MM/yyyy HH:mm"));
                                    table.Cell().Text(cliente?.Nome ?? "-");
                                    table.Cell().Text(tipoServico?.Tipo ?? "-");
                                    table.Cell().Text($"R$ {a.ValorCobrado:F2}");
                                    table.Cell().Text($"R$ {a.CustoMaterial:F2}");
                                }
                            });

                        col.Item()
                            .PaddingTop(20)
                            .Text("Registros Financeiros Avulsos")
                            .FontSize(14)
                            .Bold();

                        col.Item()
                            .PaddingTop(5)
                            .Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("Data").Bold();
                                    header.Cell().Text("Descrição").Bold();
                                    header.Cell().Text("Categoria").Bold();
                                    header.Cell().Text("Valor").Bold();
                                });

                                foreach (var f in registros)
                                {
                                    table.Cell().Text(f.Data.ToString("dd/MM/yyyy"));
                                    table.Cell().Text(f.Descricao);
                                    table.Cell().Text(f.Categoria ?? "-");
                                    table.Cell().Text(
                                        $"{(f.Tipo == TipoFinanceiro.Entrada ? "+" : "-")} R$ {f.Valor:F2}"
                                    );
                                }
                            });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Página ");
                            x.CurrentPageNumber();
                            x.Span(" de ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf();
        }
    }
}