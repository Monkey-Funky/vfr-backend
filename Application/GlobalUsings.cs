global using System.Security.Claims;
global using Domain.Entities.Retailer;  
global using MediatR;
global using FluentValidation;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.DependencyInjection;
global using System.Diagnostics;
global using System.Linq.Expressions;
global using System.Reflection;
global using Application.Interfaces;
global using Application.Behaviors;
global using Domain.Common;
global using Domain.Exceptions;
global using Shared.DTOs;

// ── Subscriptions (added P-015) ───────────────────────────────────────────
global using Domain.Entities.Subscriptions;
global using Domain.Enums;
global using Domain.Events;

// ── Payment Methods (added P-016) ─────────────────────────────────────────
global using Application.Features.PaymentMethods.Commands.AddPaymentMethod;
global using Application.Features.PaymentMethods.Commands.RemovePaymentMethod;
global using Application.Features.PaymentMethods.Commands.SetDefaultPaymentMethod;
global using Application.Features.PaymentMethods.Queries.GetPaymentMethods;