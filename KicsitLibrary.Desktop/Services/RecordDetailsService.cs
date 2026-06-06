using System;
using System.Threading.Tasks;
using KicsitLibrary.Core.Interfaces;
using KicsitLibrary.Core.Enums;
using KicsitLibrary.Desktop.ViewModels;
using KicsitLibrary.Desktop.Views;

namespace KicsitLibrary.Desktop.Services
{
    public class RecordDetailsService : IRecordDetailsService
    {
        private readonly IConsumerService _consumerService;
        private readonly ICatalogService _catalogService;
        private readonly IAuthenticationService _authenticationService;

        public RecordDetailsService(
            IConsumerService consumerService,
            ICatalogService catalogService,
            IAuthenticationService authenticationService)
        {
            _consumerService = consumerService ?? throw new ArgumentNullException(nameof(consumerService));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        }

        public void OpenMemberProfile(int memberId, MemberType memberType)
        {
            var viewModel = new ConsumerProfileViewModel(_consumerService, memberId, memberType);
            var window = new ConsumerProfileWindow(viewModel);
            window.ShowDialog();
        }

        public async Task OpenBookDetailsAsync(int bookMasterId)
        {
            var book = await _catalogService.GetBookByIdAsync(bookMasterId);
            if (book == null)
            {
                throw new InvalidOperationException("Book details could not be found.");
            }

            var viewModel = new BookFormViewModel(_catalogService, _authenticationService, book);
            var window = new BookFormWindow(viewModel);
            window.ShowDialog();
        }
    }
}
