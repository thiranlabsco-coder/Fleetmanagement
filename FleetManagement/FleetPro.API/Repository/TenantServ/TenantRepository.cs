using AutoMapper;
using FleetPro.API.Data;
using FleetPro.API.Data.Entitys;
using FleetPro.API.DTOs;
using FleetPro.API.IRepository.ITenant;
using FleetPro.API.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FleetPro.API.Repository.TenantServ
{
    public class TenantRepository : ITenantServiceRepository
    {
        private readonly ApplicationDbContext context;
        private readonly IMapper mapper;
        private readonly ITenantService _tenantService;
        private readonly IPasswordService _passwordService;

        public TenantRepository(ApplicationDbContext context, IMapper mapper, ITenantService _tenantService, IPasswordService _passwordService)
        {
            this.context = context;
            this.mapper = mapper;
            this._tenantService = _tenantService;
            this._passwordService = _passwordService;
        }

        public async Task<List<AdminListResponse>> getSuperAdminAsyc()
        {
            try
            {
                var getData = await context.Tenants.Where(x => x.IsSuperAdmin == "yes").AsNoTracking().ToListAsync();

                var adminListResponses = new List<AdminListResponse>();
                foreach (var data in getData)
                {
                    adminListResponses.Add(new AdminListResponse
                    {
                        TenantId = data.TenantId,
                        CustomerName = data.CustomerName,
                        UserName = data.UserName,
                        EmailId = data.EmailId,
                        PhoneNo = data.PhoneNo,
                        Address = data.Address,
                        GstNo = data.GstNo,
                        PanNo = data.PanNo,
                        IsSuperAdmin = data.IsSuperAdmin,
                        IsActive = data.IsActive,
                        CreatedBy = data.CreatedBy,
                        CreatedDate = data.CreatedDate,
                    });
                }

                return adminListResponses;

            }
            catch (Exception ex) {
                throw new Exception("facing error while creating the customer");
            }
        }

        public async Task<List<AdminListResponse>> getAdminDetailsAsyc()
        {
            try
            {
                var getData = await context.Tenants.Where(x => x.IsSuperAdmin == "no").AsNoTracking().ToListAsync();

                var adminListResponses = new List<AdminListResponse>();
                foreach (var data in getData)
                {
                    adminListResponses.Add(new AdminListResponse
                    {
                        TenantId = data.TenantId,
                        CustomerName = data.CustomerName,
                        UserName = data.UserName,
                        EmailId = data.EmailId,
                        PhoneNo = data.PhoneNo,
                        Address = data.Address,
                        GstNo = data.GstNo,
                        PanNo = data.PanNo,
                        IsSuperAdmin = data.IsSuperAdmin,
                        IsActive = data.IsActive,
                        CreatedBy = data.CreatedBy,
                        CreatedDate = data.CreatedDate,
                    });
                }

                return adminListResponses;

            }
            catch (Exception ex)
            {
                throw new Exception("facing error while creating the customer");
            }
        }

        public async Task<(bool Success, string Message, object? Data)> OnboardClientAsync(OnboardClientRequest request)
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                if (request == null)
                {
                    return (
                        false,
                        "Request is required.",
                        null
                    );
                }

                //var customerId = request.CustomerId.Trim();

                var customerName = request.CustomerName;

                var userName = request.UserName.Trim();

                //var customerExists = await _tenantService.CustomerIdExistsAsync(customerId);
                var createPassword = _passwordService.HashPassword(request.Password);

                //if (customerExists)
                //{
                //    return (
                //        false,
                //        "Customer ID already exists.",
                //        null
                //    );
                //}

                var userNameExists = await _tenantService.UserNameExistsAsync(userName);

                if (userNameExists)
                {
                    return (
                        false,
                        "Username already exists.",
                        null
                    );
                }

                var tenant = new TenantMst
                {
                    UserName = userName,
                    Password = createPassword,
                    CustomerName = customerName,
                    EmailId = request.EmailId,
                    PhoneNo = request.PhoneNo,
                    Address = request.Address,
                    GstNo = request.GstNo,
                    PanNo = request.PanNo,
                    IsSuperAdmin = request.IsSuperAdmin,
                    IsActive = true,
                    CreatedDate = request.CreatedDate,
                    CreatedBy = request.CreatedBy,
                    UpdatedBy = request.UpdatedBy,
                    UpdatedDate = request.UpdatedDate,
                };
                // Insert Tenant
                await _tenantService.CreateTenantAsync(tenant);

                var adminUser = new UserMst
                {
                    username = userName,
                    password = createPassword,
                    fullName = request.CustomerName,
                    emailId = request.EmailId,
                    phoneNumber = request.PhoneNo,
                    role = request.IsSuperAdmin == "no" ? 2 : 1,
                    is_active = true,
                    TenantId = tenant.TenantId,
                    createdBy = request.CreatedBy,
                    created_at = request.CreatedDate,
                    updatedBy = request.UpdatedBy,
                    updated_at = request.UpdatedDate
                };
                //Insert Admin User
                await _tenantService.CreateUserAsync(adminUser);

                // Auto-assign default sidebar menus for the new tenant
                var defaultMenus = request.IsSuperAdmin == "no"
                    ? new List<RoleMenu>
                    {
                        new RoleMenu { RoleId = 2, MenuId = "1,2,3,4,5,6,7,8,9,11", TenantId = tenant.TenantId },
                        new RoleMenu { RoleId = 3, MenuId = "1,2,3,5,7,8,9", TenantId = tenant.TenantId },
                        new RoleMenu { RoleId = 4, MenuId = "2,5", TenantId = tenant.TenantId }
                    }
                    : new List<RoleMenu>
                    {
                        new RoleMenu { RoleId = 1, MenuId = "10,6", TenantId = tenant.TenantId }
                    };

                await context.RoleMenus.AddRangeAsync(defaultMenus);
                await context.SaveChangesAsync();

                await transaction.CommitAsync();

                var responseData = new
                {
                    tenantId = tenant.TenantId,
                    customerName = tenant.CustomerName,
                    adminUserId = adminUser.userId,
                    adminUserName = adminUser.username,
                    roleId = adminUser.role
                };

                return (
                    true,
                    "Client and Admin account created successfully.",
                    responseData
                );

            }
            catch (Exception ex) {
                throw new Exception("facing error while creating the customer");
            }
        }

        public async Task<bool> updateAdminAsync(OnboardClientRequestForUpdate request)
        {
            try{
            if (request == null)
            {
                return false;
            }

            await using var transaction = await context.Database.BeginTransactionAsync();

            var existingTenant = await context.Tenants
                .SingleOrDefaultAsync(x => x.TenantId == request.TenantId);

            var existingUser = await context.UserMaster
                .FirstOrDefaultAsync(x =>
                    x.TenantId == request.TenantId && x.role != 3 && x.role != 4);

            if (existingTenant == null || existingUser == null)
            {
                return false;
            }

            existingTenant.CustomerName = request.CustomerName;
            existingTenant.EmailId = request.EmailId;
            existingTenant.PhoneNo = request.PhoneNo;
            existingTenant.Address = request.Address;
            existingTenant.GstNo = request.GstNo;
            existingTenant.PanNo = request.PanNo;
            existingTenant.IsActive = request.IsActive;
            existingTenant.UpdatedBy = request.UpdatedBy;
            existingTenant.UpdatedDate = request.UpdatedDate;

            existingUser.fullName = request.CustomerName;
            existingUser.emailId = request.EmailId;
            existingUser.phoneNumber = request.PhoneNo;
            existingUser.is_active = request.IsActive;
            existingUser.updated_at = request.UpdatedDate;
            existingUser.updatedBy = request.UpdatedBy;

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
