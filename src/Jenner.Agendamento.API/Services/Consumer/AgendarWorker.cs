using CloudNative.CloudEvents.Kafka;
using CloudNative.CloudEvents.SystemTextJson;
using Confluent.Kafka;
using Jenner.Agendamento.API.Providers;
using Jenner.Comum;
using Jenner.Comum.Models;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jenner.Agendamento.API.Services.Consumer
{
    public class AgendarWorker : KafkaConsumerBase
    {
        public ISender sender;
        public AgendarWorker(IServiceProvider serviceProvider, ISender sender) :
            base(serviceProvider, new JsonEventFormatter<Comum.Models.Carteira>())
        {
            this.sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        protected override async Task DoScopedAsync(CancellationToken cancellationToken)
        {
            if (KafkaConsumer is null)
            {
                throw new ArgumentException("For some reason the Consumer is null, this shouldn't happen.");
            }

            KafkaConsumer.Subscribe(Constants.CloudEvents.AgendarTopic);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {

                    ConsumeResult<string, byte[]> result = KafkaConsumer.Consume(cancellationToken);
                    
                    var cloudEvent = result.Message.ToCloudEvent(cloudEventFormatter);

                    if (cloudEvent.Data is Carteira carteira)
                    {
                        try 
                        {
                            AgendamentoCreate novoAgendamento = new AgendamentoCreate
                            {
                                Cpf = carteira.Cpf,
                                NomePessoa = carteira.NomePessoa,
                                DataNascimento = carteira.DataNascimento,
                                DataAgendamento = carteira.GetLatestAplicacao().DataAgendamento,
                                NomeVacina = carteira.GetLatestAplicacao().NomeVacina,
                                Dose = carteira.GetLatestAplicacao().Dose
                            };

                            Aplicacao aplicacaoResult = await sender.Send(novoAgendamento, cancellationToken);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"O valor recebido não é uma plicação válida. {e.Message}");
                        }
                    }
                }
                catch (ConsumeException e)
                {
                    Console.WriteLine($"Error occured: {e.Error.Reason}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }
            }
        }

    }
}
