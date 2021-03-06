﻿using System;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SchoolManagementSystem.Domain.Entities;
using SchoolManagementSystem.Models;
using SchoolManagementSystem.Domain;

namespace SchoolManagementSystem.Controllers
{
   
    public class VotingsController : Controller
    {
        private DbSchoolRepository repository = new DbSchoolRepository();
        private DbSchoolContext db = new DbSchoolContext();

        public ActionResult Vote(int id)
        {
            var voting = db.Votings.Find(id);
            var view = new VotingVoteView
            {
                DateTimeEnd = voting.DateTimeEnd,
                DateTimeStart = voting.DateTimeStart,
                Description = voting.Description,
                IsEnabledBlankVote = voting.IsEnabledBlankVote,
                IsForAllUsers = voting.IsForAllUsers,
                MyCandidates = voting.Candidates.ToList(),
                Remarks = voting.Remarks,
                VotingId = voting.VotingId,

            };

            return RedirectToAction("Info");
        }
        public ActionResult MyVotings()
        {
            //Buscar Usuario logiado:
            var user = db.Students.Where(u => u.PIN == User.Identity.Name).FirstOrDefault();

            //VAlidar si suario existe en la tabla users:
            if (user == null)
            {
                ModelState.AddModelError(string.Empty,"There an error with the current user. call the support.");

                return View();
            }

            //Get event votings for the correct time:
            var state = this.GetState("Open");

            //Aqui traigo todas la vtaciones open(abiertas)
            var votings = db.Votings.Where(v => v.StateId == state.StateId && v.DateTimeStart <= DateTime.Now && 
                                               v.DateTimeEnd >= DateTime.Now).
                                               Include(v => v.Candidates).
                                               Include(v => v.VotingGroups).
                                               Include(v => v.State).ToList();

            //Discart events in the wich the user already vote:
            for (int i = 0; i < votings.Count; i++)
            {
                int userId = user.StudentID;
                int votingId = votings[i].VotingId;

                var votingDetail = db.votingDetail.Where(vd => vd.VotingID == votingId && vd.UserId == userId).FirstOrDefault();

                //si es diferente de null el susuario ya voto: entonces lo elimino de la vista:
                if (votingDetail != null)
                {
                    votings.RemoveAt(i);
                }
            }

            //Discart events by group in wich the user are not included:
            for (int i = 0; i < votings.Count; i++)
            {
                if (!votings[i].IsForAllUsers)
                {
                    bool userBelongsToGroup = false;

                    foreach (var votinGroup in votings[i].VotingGroups)
                    {
                        var userGroup = votinGroup.Group.GroupMembers.Where(gm => gm.UserId == user.StudentID).FirstOrDefault();

                        if (userGroup != null)
                        {
                            userBelongsToGroup = true;
                            break;
                        }
                    }

                    if (!userBelongsToGroup)//si el usuario no pertence al grupo, lo borro:
                    {
                        votings.RemoveAt(i);
                    }

                }

                

            }

            return View(votings); 
        }

        private State GetState(string stateName)
        {
            //buscar en la tabla estado este (open)abierto:
            var state = db.States.Where(s => s.Description == stateName).FirstOrDefault();

            //valido si el estado existe:
            if (state == null)
            {
                //creo el estado open, y me aseguro que siempre lo este:
                state = new State
                {
                    Description = stateName,
                };

            db.States.Add(state);
            db.SaveChanges();
            }

            return state;

        }
        public ActionResult DeleteGroup(int id)
        {
            //bus el id o clave promaria:
            var votingGroup = db.VotingGroups.Find(id);
            if (votingGroup != null)
            {
                db.VotingGroups.Remove(votingGroup);
                db.SaveChanges();
            }

            return RedirectToAction(string.Format("Details/{0}", votingGroup.VotingId));
        }
        public ActionResult DeleteCandidate(int id)
        {
            //bus el id o clave promaria:
            var candidate = db.Candidates.Find(id);

            //Si el candidato es difenete de nullo, es que lo encontro:
            if (candidate != null)
            {
                db.Candidates.Remove(candidate);
                db.SaveChanges();
            }

            return RedirectToAction(string.Format("Details/{0}", candidate.VotingId));
        }

        [HttpGet]
        public ActionResult AddCandidate(int id)
        {
            var view = new AddCandidateView
            {
                VotingId = id,
            };
            ViewBag.Candidates = new SelectList(db.Students, "StudentID", "LastName");

            Student student = db.Students.Find(id);
            
            return View(view); 
        }

        [HttpPost]
        public ActionResult AddCandidate(AddCandidateView view)
        {
            if (ModelState.IsValid)
            {
                var candidate = db.Candidates.Where(c => c.VotingId == view.VotingId && c.StudentID == view.UserId).FirstOrDefault();

                if (candidate != null)
                {

                    ModelState.AddModelError(string.Empty, "The Candidate already belongs to voting...");
                    ViewBag.Candidates = new SelectList(db.Students, "StudentID", "LastName");
                                                          
                     return View(view);
                }

                candidate = new Candidate
                {
                    StudentID = view.UserId,
                    VotingId = view.VotingId,
                };

                db.Candidates.Add(candidate);
                db.SaveChanges();

                return RedirectToAction(string.Format("Details/{0}", view.VotingId));

            }

            ViewBag.StudentID = new SelectList(db.Students.OrderBy(u => u.FirstName).ThenBy(u => u.LastName), "StudentID", "FullName");


            return View(view);
        }
        [HttpGet]         
        public ActionResult AddGroup(int id)
        {
            ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description").ToList();
            
            // ViewBag.GroupId = new SelectList(db.Groups.OrderBy(vg => vg.Description), "GroupId", "Description").FirstOrDefault();
            var view = new AddGroupView
            {
                VotingId = id,
                
            };

            return View(view);
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddGroup(AddGroupView view)
          {           

            if (ModelState.IsValid)
            {
                var votingGroup = db.VotingGroups.Where(vg => vg.VotingId == view.VotingId && vg.GroupId == view.GroupId).FirstOrDefault();

                if (votingGroup != null)
                {

                    ModelState.AddModelError(string.Empty, "The group already belongs to voting...");
                    
                    ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description").ToList();
                    
                    return View(view);
                }

                votingGroup = new VotingGroup
                {
                  GroupId = view.GroupId,
                  VotingId = view.VotingId,
                };

                db.VotingGroups.Add(votingGroup);
                db.SaveChanges();

                return RedirectToAction(string.Format("Details/{0}", view.VotingId));
                             
            }

            ViewBag.GroupId = new SelectList(db.Groups.OrderBy(g => g.Description), "GroupId", "Description").ToList();

            return View(view);
        }

        // GET: Votings
        public ActionResult Index()
        {
            var votings = db.Votings.Include(v => v.State);
            return View(votings.ToList());
        }

        // GET: Votings/Details/5
        public ActionResult Details(int? id)
        {
            //if (id == null)
            //{
            //    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            //}

            Voting voting = db.Votings.Find(id);

            if (voting == null)
            {
                return HttpNotFound();
            }

            var view = new DetailsVotingView
            {
                Candidates = voting.Candidates.ToList(),
                CandidateWinId = voting.CandidateWinId,
                DateTimeEnd = voting.DateTimeEnd,
                DateTimeStart = voting.DateTimeStart,
                Description = voting.Description,
                IsEnabledBlankVote = voting.IsEnabledBlankVote,
                IsForAllUsers = voting.IsForAllUsers,
                QuantityBlankVotes = voting.QuantityBlankVotes,
                QuantityVotes = voting.QuantityVotes,
                Remarks = voting.Remarks,
                StateId = voting.StateId,
                VotingGroups = voting.VotingGroups.ToList(),
                VotingId = voting.VotingId,
            };

            return View(view);
        }
        // GET: Votings/Create
        public ActionResult Create()
        {
            ViewBag.StateId = new SelectList(db.States, "StateId", "Description");

            var view = new VotingView
            {
                DateStart = DateTime.Now,
                DateEnd = DateTime.Now,
                
            };

            return View(view);
        }

        // POST: Votings/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VotingView view)
        {
            if (ModelState.IsValid)
            {
                //crear objeto voting con relacaión a VotingView:
                var voting = new Voting
                {
                    DateTimeStart = view.TimeStart.AddHours(view.TimeStart.Hour).AddMinutes(view.TimeStart.Minute),
                    DateTimeEnd   = view.TimeEnd.AddHours(view.TimeEnd.Hour).AddMinutes(view.TimeEnd.Minute),
                    Description = view.Description,
                    IsEnabledBlankVote = view.IsEnabledBlankVote,
                    IsForAllUsers = view.IsForAllUsers,
                    Remarks = view.Remarks,
                    StateId = view.StateId,


                };

                db.Votings.Add(voting);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", view.StateId);
            return View(view);
        }

        // GET: Votings/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Voting voting = db.Votings.Find(id);
            if (voting == null)
            {
                return HttpNotFound();
            }

            var view = new VotingView
            {
              DateEnd = voting.DateTimeEnd,
              DateStart = voting.DateTimeStart,
              Description = voting.Description,
              IsEnabledBlankVote = voting.IsEnabledBlankVote,
              IsForAllUsers = voting.IsForAllUsers,
              Remarks = voting.Remarks,
              StateId = voting.StateId,
              TimeEnd = voting.DateTimeEnd,
              TimeStart = voting.DateTimeStart,
              VotingId = voting.VotingId,
            };

            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", voting.StateId);
            return View(view);
        }

        // POST: Votings/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(VotingView view)
        {
            if (ModelState.IsValid)
            {

                //crear objeto voting con relacaión a VotingView:
                var voting = new Voting
                {
                    VotingId = view.VotingId,
                    DateTimeStart = view.TimeStart.AddHours(view.TimeStart.Hour).AddMinutes(view.TimeStart.Minute),
                    DateTimeEnd = view.TimeEnd.AddHours(view.TimeEnd.Hour).AddMinutes(view.TimeEnd.Minute),
                    Description = view.Description,
                    IsEnabledBlankVote = view.IsEnabledBlankVote,
                    IsForAllUsers = view.IsForAllUsers,
                    Remarks = view.Remarks,
                    StateId = view.StateId,


                };

                db.Entry(voting).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.StateId = new SelectList(db.States, "StateId", "Description", view.StateId);
            return View(view);
        }

        // GET: Votings/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Voting voting = db.Votings.Find(id);
            if (voting == null)
            {
                return HttpNotFound();
            }
            return View(voting);
        }

        // POST: Votings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Voting voting = db.Votings.Find(id);
            db.Votings.Remove(voting);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
