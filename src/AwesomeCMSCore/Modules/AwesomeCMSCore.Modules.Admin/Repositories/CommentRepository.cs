using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using AutoMapper;
using AwesomeCMSCore.Modules.Admin.ViewModels;
using AwesomeCMSCore.Modules.Entities.Entities;
using AwesomeCMSCore.Modules.Entities.Enums;
using AwesomeCMSCore.Modules.Entities.ViewModel;
using AwesomeCMSCore.Modules.Helper.Services;
using AwesomeCMSCore.Modules.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AwesomeCMSCore.Modules.Admin.Repositories
{
    public class CommentRepository : ICommentRepository
    {
        private readonly IUserService _userService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly string _currentUserId;

        public CommentRepository(
            IUserService userService,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _userService = userService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUserId = _userService.GetCurrentUserGuid();
        }

        public async Task<CommentDefaultViewModel> GetAllComments()
        {
            var comments = await _unitOfWork.Repository<Comment>().Query()
                .Include(x => x.User)
                .Include(c => c.Post)
                .Select(x => new CommentViewModel
                {
                    User = _mapper.Map<User, UserViewModel>(x.User),
                    Post = x.Post,
                    Comment = _mapper.Map<Comment, CommentDto>(x),
                })
                .ToListAsync();

            var viewModel = new CommentDefaultViewModel
            {
                AllComments = comments,
                NumberOfComments = comments.Count(),
                ApprovedComments = GetCommentsByStatus(comments, CommentStatus.Approved),
                NumberOfApprovedComments = CountComment(comments, CommentStatus.Approved),
                PendingComments = GetCommentsByStatus(comments, CommentStatus.Pending),
                NumberOfPendingComments = CountComment(comments, CommentStatus.Pending),
                SpamComments = GetCommentsByStatus(comments, CommentStatus.Spam),
                NumberOfSpamComments = CountComment(comments, CommentStatus.Spam),
                DeletedComments = GetCommentsByStatus(comments, CommentStatus.Trash),
                NumberOfDeletedComments = CountComment(comments, CommentStatus.Trash)
            };

            return viewModel;
        }

        public async Task<bool> UpdateCommentStatus(int commentId, CommentStatus commentStatus)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    var commentToUpdate = await _unitOfWork.Repository<Comment>().Query().Where(cm => cm.Id == commentId).SingleOrDefaultAsync();
                    commentToUpdate.CommentStatus = commentStatus;
                    await _unitOfWork.Repository<Comment>().UpdateAsync(commentToUpdate);
                    transaction.Complete();
                    return true;
                }
                catch (Exception)
                {
                    _unitOfWork.Rollback();
                    return false;
                }
            }
        }

        public async Task<bool> ReplyComment(CommentReplyViewModel replyViewModel)
        {
            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                try
                {
                    var post = await _unitOfWork.Repository<Post>().Query().Where(cm => cm.Id == replyViewModel.PostId).SingleOrDefaultAsync();
                    var comment = new Comment
                    {
                        ParentId = replyViewModel.ParentId,
                        Post = post,
                        CommentStatus = CommentStatus.Pending,
                        Content = replyViewModel.CommentBody,
                        User = await _userService.GetCurrentUserAsync()
                    };

                    await _unitOfWork.Repository<Comment>().AddAsync(comment);
                    transaction.Complete();
                    return true;
                }
                catch (Exception)
                {
                    _unitOfWork.Rollback();
                    return false;
                }
            }
        }

        private static int CountComment(IEnumerable<CommentViewModel> comments, CommentStatus commentStatus)
        {
            return comments.Count(cm => cm.Comment.CommentStatus.Equals(commentStatus));
        }

        private static IEnumerable<CommentViewModel> GetCommentsByStatus(IEnumerable<CommentViewModel> comments, CommentStatus commentStatus)
        {
            return comments.Where(cm => cm.Comment.CommentStatus == commentStatus);
        }
    }
}