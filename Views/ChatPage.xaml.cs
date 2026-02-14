using LLMChat.ViewModels;

namespace LLMChat.Views
{
    //Página principal del chat
    public partial class ChatPage : ContentPage
    {
        private readonly ChatViewModel vm;

        public ChatPage(ChatViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            vm = viewModel;

#if WINDOWS
            //Desactivar animaciones de aparición de mensajes
            MessagesCollection.HandlerChanged += (s, e) =>
            {
                if (MessagesCollection.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ListView lista)
                    lista.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
            };
#endif

            //Auto-scroll al recibir nuevos mensajes
            vm.Messages.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                    && vm.Messages.Count > 0)
                {
                    ScrollAlFinal(50);
                    ScrollAlFinal(200);
                    ScrollAlFinal(500);
                }
            };
        }

        //Hacer scroll al último mensaje con retardo
        private void ScrollAlFinal(int retardoMs)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(retardoMs);
                try
                {
                    var total = vm.Messages.Count;
                    if (total == 0) return;

#if WINDOWS
                    //Acceder al ScrollViewer nativo para scroll fiable
                    if (MessagesCollection.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ListView lista)
                    {
                        var sv = BuscarScrollViewer(lista);
                        if (sv != null)
                        {
                            sv.UpdateLayout();
                            sv.ChangeView(null, sv.ScrollableHeight, null, true);
                        }
                        else
                        {
                            MessagesCollection.ScrollTo(total - 1, position: ScrollToPosition.End, animate: false);
                        }
                    }
                    else
                    {
                        MessagesCollection.ScrollTo(total - 1, position: ScrollToPosition.End, animate: false);
                    }
#else
                    MessagesCollection.ScrollTo(total - 1, position: ScrollToPosition.End, animate: false);
#endif
                }
                catch { }
            });
        }

#if WINDOWS
        //Buscar ScrollViewer recursivamente en el árbol visual nativo
        private static Microsoft.UI.Xaml.Controls.ScrollViewer? BuscarScrollViewer(
            Microsoft.UI.Xaml.DependencyObject padre)
        {
            var hijos = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < hijos; i++)
            {
                var hijo = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(padre, i);
                if (hijo is Microsoft.UI.Xaml.Controls.ScrollViewer sv)
                    return sv;

                var resultado = BuscarScrollViewer(hijo);
                if (resultado != null)
                    return resultado;
            }
            return null;
        }
#endif
    }
}
