using System.Threading.Tasks;
using KicsitLibrary.Core.Enums;

namespace KicsitLibrary.Desktop.Services
{
    public interface IRecordDetailsService
    {
        void OpenMemberProfile(int memberId, MemberType memberType);
        Task OpenBookDetailsAsync(int bookMasterId);
    }
}
