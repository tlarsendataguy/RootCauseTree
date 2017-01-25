﻿using System;
using System.Collections.Generic;
using System.Text;

namespace com.PorcupineSupernova.RootCauseTreeCore
{
    class AddLinkCommand : IRootCauseCommand
    {
        private Node _StartNode;
        private Node _EndNode;

        public AddLinkCommand(Node startNode, Node endNode) : this(startNode, endNode, false) { }

        public AddLinkCommand(Node startNode,Node endNode,bool executeImmediately)
        {
            _StartNode = startNode;
            _EndNode = endNode;
            if (executeImmediately) { Execute(); }
        }

        public void Execute()
        {
            _StartNode.AddNode(_EndNode);
            _EndNode.AddParent(_StartNode);
        }

        public void Undo()
        {
            _StartNode.RemoveNode(_EndNode);
            _EndNode.RemoveParent(_StartNode);
        }
    }
}