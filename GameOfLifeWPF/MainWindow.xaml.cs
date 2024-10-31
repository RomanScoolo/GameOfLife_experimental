using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;

namespace GameOfLifeWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const int elemSize = 25;
        const int gridSize = 30;

        static int[,] cells = new int[gridSize, gridSize];
        static int[,] future = new int[gridSize, gridSize];

        const int countTypes = 8;
        static Color emptyColor = Colors.LightCyan;
        static Color cellColor = Colors.Black;
        static Color foodColor = Colors.LightGreen;
        static Color hunterColor = Colors.Blue;
        static Color farmerColor = Colors.Green;
        static Color moverColor = Colors.Aquamarine;
        static Color wallColor = Colors.Brown;
        static Color antColor = Colors.Red;
        static Color[] cellsColors = {emptyColor, cellColor, foodColor, hunterColor, farmerColor, moverColor, wallColor, antColor};

        const int emptyCell = 0;  // what-else
        const int normalCell = 1; // community + can survive if eating cell
        const int foodCell = 2;   // do nothing, can be eaten
        const int hunterCell = 3; // moves towards other cells (mid perimeter) + eats food or normal cell
        const int farmerCell = 4; // like normal, but also creates food nearby at random position
        const int moverCell = 5;  // like normal, but can move towards other cells (small perimeter)
        const int wallCell = 6;   // do nothing, represents wall or ground
        const int antCell = 7;    // can move on the ground, otherwise falls down by 1 cell, seeks food
        // animal cell = like food, but can move randomly, or look for food cell (within small perimeter)
        // .. normal cell can also eat animal cell
        // 
        // Improvements:
        // - add size, ie mover can surive 3 moves, and stamina recharges after eating or being in community
        // 

        static int perimeter = 5; // how far cell can see other cells

        DispatcherTimer dispatcherTimer;
        static bool running = false;
        static int interval = 100; // how frequently grid will refresh in miliseconds
        static int genCount = 0; // how many times generation evolved

        static Random rand = new Random();
        static int randomMax = 50; // percentage for random generation, default 50%

        // ant and food
        static bool antGame = false;
        static int foodX, foodY;
        static int antX, antY;
        static int sourceX = -1; // hive for ants
        static int sourceY = -1; 
        static int xFood, yFood;     // direction towards food
        static int difX, difY;       // absolute distance for each axis between food and ant
        static int directCell, undernCell, nextxCell, underxCell, underxxCell, nextxdCell, abovedCell; // cells evaluation
        static bool foundFood  = false;
        static bool stoned = false;
        static bool sourced = false;

        // seeker and labyrint (find food)
        static bool seekerGame = false;
        static int seekerX, seekerY;
        static int[,] seekPath = new int[gridSize * gridSize, 2];
        static int[,] seekMap = new int[gridSize, gridSize];
        static int seekPathPos = 0;

        // optimal path finder
        static int[,] path = new int[gridSize, 2];
        static int pathLength = 0;
        const int maxPaths = 1000;  // limit checked paths
        static bool[,] paths = new bool[maxPaths, gridSize];     // different paths, each item is either diagonal o straight move
        static int[,] pathLengths = new int[gridSize,2];         // length and how many ants used
        static int pathses = 0;                                  // how many different paths got investigated
        static bool[] ts = new bool[gridSize];

        public MainWindow()
        {
            InitializeComponent();

            Loaded += new RoutedEventHandler(MainWindow_Loaded);

            //  DispatcherTimer setup
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, interval);
            dispatcherTimer.Start();
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AddCells(new Size(gridSize, gridSize));
        }

        private void AddCells(Size recSize)
        {
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                {
                    cells[i, j] = emptyCell;
                    future[i, j] = emptyCell;
                }

            // UniGrid.Columns = (int)(UniGrid.ActualWidth / recSize.Width);
            // UniGrid.Rows = (int)(UniGrid.ActualHeight / recSize.Height);
            UniGrid.Columns = gridSize;
            UniGrid.Rows = gridSize;
            UniGrid.Width = UniGrid.Columns * (elemSize + 1);
            UniGrid.Height = UniGrid.Rows * (elemSize + 1);

            for (int i = 0; i < UniGrid.Columns * UniGrid.Rows; i++)
            {
                UniGrid.Children.Add(new Ellipse { Fill = new SolidColorBrush(emptyColor), Margin = new Thickness(1) });
            }

            genCount = 0;
            TimerInterval.Text = interval.ToString();
        }

        private void gridMouseUp(object sender, MouseButtonEventArgs e)
        {
            string s = "";
            int x, y, i;

            s = e.GetPosition(this).ToString();
            // MessageBox.Show("You clicked me at " + s);

            x = Int32.Parse(s.Split(',')[0]);
            y = Int32.Parse(s.Split(',')[1]);
            // Return the general transform for the specified visual object.
            GeneralTransform generalTransform1 = UniGrid.TransformToAncestor(this);

            // Retrieve the point value relative to the parent.
            Point currentPoint = generalTransform1.Transform(new Point(0, 0));

            x = (x - (int)(currentPoint.X + 0.5)) / (elemSize + 1);
            y = (y - (int)(currentPoint.Y + 0.5)) / (elemSize + 1);

            i = x + y * gridSize;

            // MessageBox.Show("X:" + x.ToString() + " Y:" + y.ToString() + " index = " + i.ToString());

            cells[y, x]++;
            if (cells[y, x] > (countTypes - 1)) // interim finish rotation with hunter cell-type
            {
                cells[y, x] = emptyCell;
                noCells.Text = (Int32.Parse(noCells.Text) - 1).ToString();
            }
            if (cells[y, x] == normalCell)
                noCells.Text = (Int32.Parse(noCells.Text) + 1).ToString();

            UniGrid.Children.RemoveAt(i);
            UniGrid.Children.Insert(i, new Ellipse { Fill = new SolidColorBrush(cellsColors[cells[y,x]]), Margin = new Thickness(1) });

            // MessageBox.Show("X: " + x.ToString() + "Y: " + y.ToString() + " cell = " + cells[y,x].ToString());
        }

        private int tested(int t)
        {
            if (t < 0)
                t = gridSize + t;
            if (t >= gridSize)
                t = t - gridSize;
            return t;
        }

        private bool eaten(int y, int x)
        {
            if (cells[tested(y), tested(x)] == foodCell)
            {
                cells[tested(y), tested(x)] = emptyCell;
                future[tested(y), tested(x)] = emptyCell;
                return true;
            }
            else
                return false;
        }

        // currently not used method, foreseen for learning cells
        static int direction(int from, int to)
        {
            int dir = 0;
            if (from > to)
                dir = -1;
            if (from < to)
                dir = 1;
            return dir;
        }

        // currently not used method, foreseen for learning cells
        static int distance(int from_x, int from_y, int to_x, int to_y)
        {
            return (int)Math.Sqrt((from_x - to_x) * (from_x - to_x) + (from_y - to_y) * (from_y - to_y));
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {

            // Updating the TextBlock which displays the current second
            // TimerValue.Text = DateTime.Now.Second.ToString();

            // create next generation
            if (running)
            {
                int c = 0;
                int dx, dy, ddx, ddy;

                // hunter cells 1st
                for (int i = 0; i < gridSize; i++)
                    for (int j = 0; j < gridSize; j++)
                    {
                        // hunter cells can move towards other eatable cells, if no cell visible within perimeter then become normal cell
                        bool hunted = false;
                        if (cells[i, j] == hunterCell) // must be hunter
                        {
                            c++;
                            int pp = 1;
                            do
                            {
                                for (int ii = (i - pp); ii < (i + pp + 1); ii++)
                                    if (!hunted)
                                        for (int jj = (j - pp); jj < (j + pp + 1); jj++)
                                        {
                                            //evaluate around, skip own position, only hunt for 1st occurence
                                            if ((ii == i) & (jj == j))
                                                jj++;
                                            if (!hunted & ((cells[tested(ii), tested(jj)] == normalCell) | (cells[tested(ii), tested(jj)] == foodCell) | (cells[tested(ii), tested(jj)] == farmerCell) | (cells[tested(ii), tested(jj)] == moverCell)))
                                            {
                                                hunted = true;
                                                
                                                // check which direction to move
                                                dy = 0;
                                                dx = 0;
                                                if (ii < i)
                                                    dy = -1;
                                                if (ii > i)
                                                    dy = 1;
                                                if (jj < j)
                                                    dx = -1;
                                                if (jj > j)
                                                    dx = 1;
                                                
                                                // if too far, then come closer to bigger distant dimension
                                                ddy = Math.Abs(tested(ii) - i);
                                                ddx = Math.Abs(tested(jj) - j);
                                                if (ddx > ddy)
                                                    dy = 0;
                                                else if (ddx < ddy)
                                                    dx = 0;

                                                if (pp == 1) // stay at the same place for nearby cells
                                                {
                                                    future[i, j] = hunterCell;
                                                    if ((cells[tested(ii), tested(jj)] == normalCell) | (cells[tested(ii), tested(jj)] == foodCell))
                                                    {
                                                        cells[tested(ii), tested(jj)] = emptyCell; // eat nearby normal or food cell
                                                        future[tested(ii), tested(jj)] = emptyCell;
                                                    }
                                                }
                                                else
                                                {
                                                    if (future[tested(i + dy), tested(j + dx)] == emptyCell) //move towards
                                                        future[tested(i + dy), tested(j + dx)] = hunterCell;
                                                    else
                                                        future[i, j] = hunterCell;
                                                }
                                            }
                                        }
                                pp++;
                            } while ((pp <= perimeter) & (!hunted));


                            if (!hunted) // nothing found, convert to normal
                            {
                                cells[i, j] = emptyCell;
                                future[i, j] = normalCell;
                            }
                        }
                    }

                // ant cells - move towards food, if no wall under it, then fall 1 cell and keep attached
                // .. find position of food (initially lets consider single food in the field)
                // .. if food is next to ant then found it (and do nothing)
                // .. if no ground (wall or ant) then cannot move (and so do nothing)
                // .. calculate direction
                // .. check cell in food-direction - if having wall or ant underneeth then move there (except its him)
                // .. check cell in x-axis of food direction - if wall or ant underneeth then move there
                // .. check cell in x-axis of food direction - if ant or wall then stay there
                // .. check cell under next x-axis of food direction - if empty then move there
                // .. stay
                //
                // issues 
                // - if there is obstacle taller as food position
                // - if food is higher then diagonally achievable
                // - not using intermediate platforms to help = that would lead to more complex evaluation
                // - setup loose connections and allow filling gaps diagonally not just on the floor (and so allow alternating moves, 2 directions)
                // - ideally calculate/generate multiple paths and choose the shortest one / one with the least ants used
                //
                antX = -1;
                antY = -1;
                for (int i = 0; i < gridSize; i++)
                    for (int j = 0; j < gridSize; j++)
                        if (cells[i, j] == antCell)
                        {
                            antX = j;
                            antY = i;
                            antGame = true;
                            i = gridSize;
                            j = gridSize;
                        }
                foodX = -1;
                foodY = -1;
                if (antGame | sourced)
                    for (int i = 0; i < gridSize; i++)
                        for (int j = 0; j < gridSize; j++)
                            if (cells[i, j] == foodCell)
                            {
                                foodX = j;
                                foodY = i;
                            }

                if (!foundFood & !sourced & antGame) // if have not setup source yet
                {
                    sourceX = antX;
                    sourceY = antY;
                    sourced = true;
                    // calculate ideal path between ant and food
                }
                if ((antX < 0) & antGame) // there must be at least 1 ant to move, if not then generate from source (if any)
                {
                    antX = sourceX;
                    antY = sourceY;
                    cells[antY, antX] = antCell;
                }
                if (!foundFood & (antX > -1))
                    {
                        foundFood = false;
                    if (distance(antX, antY, foodX, foodY) < 2)
                    {
                        foundFood = true;
                        future[antY, antX] = antCell; // keep at the same place
                        running = false;
                        TimerStatus.Text = "Stopped";
                    }
                    else if ((cells[antY + 1, antX] == wallCell) | (cells[antY + 1, antX] == antCell)) // only evaluate move possibility if standing on wall or ant
                    {
                        xFood = direction(antX, foodX);
                        yFood = direction(antY, foodY);

                        directCell = cells[antY + yFood, antX + xFood];
                        undernCell = cells[antY + yFood + 1, antX + xFood];
                        nextxCell = cells[antY, antX + xFood];
                        underxCell = cells[antY + 1, antX + xFood];
                        underxxCell = cells[antY + 2, antX + xFood];
                        nextxdCell = cells[antY + yFood + yFood + 1, antX + xFood + xFood];
                        abovedCell = cells[antY - 1, antX + xFood];

                        // mravec spaja s hore, s dole, rovno
                        // mravec ide rovno, stupa, klesa
                        /*
                        if ((nextxdCell == wallCell) & (nextxCell != wallCell))
                        {
                            future[antY + 1 + yFood, antX + xFood] = wallCell;
                            stoned = true;
                        }
                        else if ((yFood == -1) & (directCell == emptyCell) & (undernCell == wallCell))
                            future[antY + yFood, antX + xFood] = antCell;
                        */

                        if ((Math.Abs(foodX - antX - xFood) == Math.Abs(foodY - antY)) & (directCell == emptyCell) & (nextxCell != wallCell))
                        {
                            future[antY, antX + xFood] = wallCell;
                            stoned = true;
                        }
                        else if ((directCell == emptyCell) & (nextxdCell == wallCell) & (nextxCell != wallCell)) // connect with platform in direction of food
                        {
                            future[antY, antX + xFood] = wallCell;
                            stoned = true;
                        }
                        else if ((directCell == emptyCell) & (undernCell == wallCell) & (antX != foodX)) // just move in direction
                        {
                            future[antY + yFood, antX + xFood] = antCell;
                        }
                        else if ((abovedCell == emptyCell) & (nextxCell == wallCell) & (antX != foodX)) // hop over
                        {
                            future[antY - 1, antX + xFood] = antCell;
                        }
                        else if ((directCell == wallCell) & (yFood != 0))
                        {
                            future[antY, antX] = wallCell; // create connection with wall in direction
                            stoned = true;
                        }
                        else if ((nextxCell == emptyCell) & (underxCell == wallCell)) // walk on platform
                            future[antY, antX + xFood] = antCell;
                        else if ((nextxCell == wallCell) & (underxCell == wallCell)) // keep at the same place when having roadblock
                        {
                            future[antY, antX] = wallCell; 
                            stoned = true;
                        }
                        else if ((underxCell == emptyCell) & (underxxCell == emptyCell))  // fall down to empty place next to it
                        {
                            future[antY + 1, antX + xFood] = wallCell;
                            stoned = true;
                        }
                        else if ((underxCell == emptyCell) & (underxxCell == wallCell)) // move down
                        {
                            future[antY + 1, antX + xFood] = antCell;
                        }
                        else
                        {
                            future[antY, antX] = wallCell; // keep at the same place
                            stoned = true;
                        }
                    }
                    else
                        future[antY, antX] = antCell; // keep at the same place
                }
                else if (sourced)
                    future[antY, antX] = antCell; // keep at the same place

                // main cells evaluationcycle
                for (int i = 0; i < gridSize; i++)
                    for (int j = 0; j < gridSize; j++)
                    {
                        // count neighbors of cell (only normal cells are counted)
                        int n = 0;
                        if (cells[tested(i - 1), tested(j - 1)] == normalCell)
                            n++;
                        if (cells[tested(i - 1), tested(j)] == normalCell)
                            n++;
                        if (cells[tested(i - 1), tested(j + 1)] == normalCell)
                            n++;
                        if (cells[tested(i), tested(j - 1)] == normalCell)
                            n++;
                        if (cells[tested(i), tested(j + 1)] == normalCell)
                            n++;
                        if (cells[tested(i + 1), tested(j - 1)] == normalCell)
                            n++;
                        if (cells[tested(i + 1), tested(j)] == normalCell)
                            n++;
                        if (cells[tested(i + 1), tested(j + 1)] == normalCell)
                            n++;

                        // classic evaluation of Game of Life rules
                        if ((cells[i, j] == normalCell) & ((n == 2) | (n == 3)))
                        {
                            future[i, j] = normalCell;
                            c++;
                        }
                        if ((cells[i, j] == emptyCell) & (n == 3))
                        {
                            future[i, j] = normalCell;
                            c++;
                        }

                        // cell can eat food cell to survive
                        if ((cells[i, j] == normalCell) & (n < 2))
                        {
                            if (eaten(i - 1, j - 1))
                                future[i, j] = normalCell;
                            else if (eaten(i -  1, j))
                                future[i, j] = normalCell;
                            else if (eaten(i - 1, j + 1))
                                future[i, j] = normalCell;
                            else if (eaten(i, j - 1))
                                future[i, j] = normalCell;
                            else if (eaten(i, j + 1))
                                future[i, j] = normalCell;
                            else if (eaten(i + 1, j - 1))
                                future[i, j] = normalCell;
                            else if (eaten(i + 1, j))
                                future[i, j] = normalCell;
                            else if (eaten(i + 1, j + 1))
                                future[i, j] = normalCell;
                        }

                        // food itself stays - no action, just exists
                        if ((cells[i, j] == foodCell) & (future[i, j] == emptyCell))
                        {
                            future[i, j] = foodCell;
                            c++;
                        }

                        // interim - farmer - no action
                        if ((cells[i, j] == farmerCell) & (future[i, j] == emptyCell))
                        {
                            future[i, j] = farmerCell;
                            c++;
                        }

                        // interim - mover - no action
                        if ((cells[i, j] == moverCell) & (future[i, j] == emptyCell))
                        {
                            future[i, j] = moverCell;
                            c++;
                        }

                        // wall itself stays - no action, just exists
                        if ((cells[i, j] == wallCell) & (future[i, j] == emptyCell))
                        {
                            future[i, j] = wallCell;
                            c++;
                        }

                        // interim - ant - no action
                        /* if ((cells[i, j] == antCell) & (future[i, j] == emptyCell))
                        {
                            future[i, j] = antCell;
                            c++;
                        }
                        */

                    }

                noCells.Text = c.ToString();
                if (c == 0)
                {
                    running = false;
                    TimerStatus.Text = "Stopped";
                }
                genCount++;
                TimerValue.Text = genCount.ToString();

                UniGrid.Children.RemoveRange(0, UniGrid.Children.Count);

                for (int i = 0; i < UniGrid.Columns * UniGrid.Rows; i++)
                    UniGrid.Children.Add(new Ellipse { Fill = new SolidColorBrush(cellsColors[future[i / gridSize, i % gridSize]]), Margin = new Thickness(1) });

                for (int i = 0; i < gridSize; i++)
                    for (int j = 0; j < gridSize; j++)
                    {
                        cells[i, j] = future[i, j];
                        future[i, j] = emptyCell;
                    }
            }

            // Forcing the CommandManager to raise the RequerySuggested event
            CommandManager.InvalidateRequerySuggested();
        }

        private void startGen(object sender, MouseButtonEventArgs e)
        {
            running = true;
            TimerStatus.Text = "Running";
            if (Int32.TryParse(TimerInterval.Text, out interval))
            {
                if (interval < 100)
                    interval = 100;
            }
            else
                    interval = 100;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, interval);
            TimerInterval.Text = interval.ToString();
        }

        private void stopGen(object sender, MouseButtonEventArgs e)
        {
            running = false;
            TimerStatus.Text = "Stopped";
        }

        private void clearing()
        {
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                {
                    cells[i, j] = emptyCell;
                    future[i, j] = emptyCell;
                }

            noCells.Text = "0";
            genCount = 0;
            TimerValue.Text = genCount.ToString();

            foundFood = false;
            stoned = false;
            sourced = false;

            UniGrid.Children.RemoveRange(0, UniGrid.Children.Count);
        }

        private void displaying()
        {
            for (int i = 0; i < UniGrid.Columns * UniGrid.Rows; i++)
                UniGrid.Children.Add(new Ellipse { Fill = new SolidColorBrush(cellsColors[cells[i / gridSize, i % gridSize]]), Margin = new Thickness(1) });
        }

        private void saveGen(object sender, MouseButtonEventArgs e)
        {
            string cesta = gridName.Text;
            XmlTextWriter zapisovac = new XmlTextWriter(cesta, Encoding.UTF8);
            zapisovac.Formatting = Formatting.Indented;
            zapisovac.WriteStartDocument();
            zapisovac.WriteStartElement("bunky");
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                {
                    if (cells[i, j] > 0)
                    {
                        zapisovac.WriteStartElement("bunka");
                        zapisovac.WriteStartElement("typ");
                        zapisovac.WriteString(cells[i, j].ToString());
                        zapisovac.WriteEndElement();
                        zapisovac.WriteStartElement("position");
                        zapisovac.WriteAttributeString("x", j.ToString());
                        zapisovac.WriteAttributeString("y", i.ToString());
                        zapisovac.WriteEndElement(); // typ
                        zapisovac.WriteEndElement(); // bunka
                    }
                }
            zapisovac.WriteEndDocument();
            zapisovac.Close();
        }

        private void loadGen(object sender, MouseButtonEventArgs e)
        {
            XmlDocument doc = new XmlDocument();
            string cesta = gridName.Text;
            doc.Load(cesta);

            clearing();
            int c = 0;
            int cc, x, y;

            XmlNode hlavniNod = doc.DocumentElement;
            foreach(XmlNode podNod in hlavniNod.ChildNodes)
            {
                cc = emptyCell;
                x = -1;
                y = -1;
                foreach (XmlNode podpodNod in podNod.ChildNodes)
                {
                    if (podpodNod.Name == "typ")
                    {
                        cc = Int32.Parse(podpodNod.ChildNodes[0].Value);
                    }
                    if (podpodNod.Name == "position")
                    {
                        foreach (XmlAttribute atribut in podpodNod.Attributes)
                        {
                            if (atribut.Name == "x")
                                x = Int32.Parse(atribut.Value);

                            if (atribut.Name == "y")
                                y = Int32.Parse(atribut.Value);
                        }
                    }
                }
                if (cc != emptyCell)
                {
                    cells[y, x] = cc;
                    c++;
                }
            }

            displaying();

            noCells.Text = c.ToString();
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void load2Gen(object sender, MouseButtonEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".xml";
            dlg.Filter = "XML Files (*.xml)|*.xml|Text Files (*.txt)|*.txt|All files (*.*)|*.*";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                gridName.Text = filename;
            }

            loadGen(sender, e);
        }

        private void seekGen(object sender, MouseButtonEventArgs e)
        {
            // check food around, write to map
            // check direction
            // mark free moves into map
            // avoid no free moves
            // mark walls into map
            // move to direction
            // remember the path taken
            // if not possible then move around
            // if not possible then mark bad spot, move back and there look around

            // locate seeker
            seekerX = -1;
            seekerY = -1;
            seekerGame = false;
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    if (cells[i, j] == moverCell)
                    {
                        seekerX = j;
                        seekerY = i;
                        seekerGame = true;
                        i = gridSize;
                        j = gridSize;
                    }
            // clear map, path
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    seekMap[i, j] = emptyCell;
            for (int i = 0; i < gridSize * gridSize; i++)
            {
                seekPath[i, 0] = -1;
                seekPath[i, 1] = -1;
            }
            seekPathPos = 0;
            seekPath[seekPathPos, 0] = seekerX;
            seekPath[seekPathPos, 1] = seekerY;
            // locate food
            foodX = -1;
            foodY = -1;
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    if (cells[i, j] == foodCell)
                    {
                        foodX = j;
                        foodY = i;
                        seekMap[i, j] = foodCell;

                        i = gridSize;
                        j = gridSize;
                    }
            xFood = 0;
            yFood = 0;
            if (foodX > seekerX)
                xFood = 1;
            else if (foodX < seekerX)
                xFood = -1;
            if (foodY > seekerY)
                yFood = 1;
            else if (foodY < seekerY)
                yFood = -1;
            difX = Math.Abs(foodX - seekerX);
            difY = Math.Abs(foodY - seekerY);

            while ((distance(foodX, foodY, seekerX, seekerY) > 1) & seekerGame)
            {
                if (difX > difY)
                {
                    if (cells[seekerY, seekerX + xFood] == emptyCell)
                    {
                        seekPathPos++;
                        seekerX = seekerX + xFood;

                        seekPath[seekPathPos, 1] = seekerY;
                        seekPath[seekPathPos, 0] = seekerX;
                        cells[seekerY, seekerX] = moverCell;
                    }
                }
                else if (difX < difY)
                {
                    if (cells[seekerY + yFood, seekerX] == emptyCell)
                    {
                        seekPathPos++;
                        seekerY = seekerY + yFood;

                        seekPath[seekPathPos, 1] = seekerY;
                        seekPath[seekPathPos, 0] = seekerX;
                        cells[seekerY, seekerX] = moverCell;
                    }
                }
                else
                {
                    if (cells[seekerY + yFood, seekerX + xFood] == emptyCell)
                    {
                        seekPathPos++;
                        seekerX = seekerX + xFood;
                        seekerY = seekerY + yFood;

                        seekPath[seekPathPos, 1] = seekerY;
                        seekPath[seekPathPos, 0] = seekerX;
                        cells[seekerY, seekerX] = moverCell;
                    }
                }

                // redraw
                UniGrid.Children.RemoveRange(0, UniGrid.Children.Count);
                displaying();
                System.Threading.Thread.Sleep(interval);
            }
        }

        private void pathGen(object sender, MouseButtonEventArgs e)
        {
            // draw path between 2 normal cells (under them with wall cell)
            bool pathFound = false;
            int pathX1 = -1, pathY1 = -1, pathX2 = -1, pathY2 = -1;
            int dx, dy, xx, yy, z, w;

            // find 2 non-empty cells
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    if (!pathFound)
                    {
                        if (cells[i, j] == normalCell)
                        {
                            if (pathX1 == -1)
                            {
                                pathX1 = j;
                                pathY1 = i;
                            }
                            else if (pathX2 == -1)
                            {
                                pathX2 = j;
                                pathY2 = i;
                                pathFound = true;
                            }
                        }
                    }

            // calculate path
            xx = pathX1;
            yy = pathY1;
            dx = (int)Math.Abs(pathX2 - pathX1);
            dy = (int)Math.Abs(pathY2 - pathY1);
            z = 0;
            w = -1; // assuming going up (so lower axis)

            if (dx > dy)
            {
                z = dx - dy;
                if (pathX1 > pathX2)
                {
                    pathX1 = pathX2;
                    pathY1 = pathY2;
                    pathX2 = xx;
                    pathY2 = yy;
                    xx = pathX1;
                    yy = pathY1;
                }
                if (pathY2 > pathY1) // change direction
                    w = 1;
                for (int m = 0; m < dx + 1; m++)
                {
                    cells[yy, xx + m] = wallCell;
                    z = z - dy;
                    if (z <= 0)
                    {
                        yy = yy + w;
                        z = z + dx;
                    }
                }
            }
            else
            {
                z = dy - dx;
                if (pathY1 > pathY2)
                {
                    pathX1 = pathX2;
                    pathY1 = pathY2;
                    pathX2 = xx;
                    pathY2 = yy;
                    xx = pathX1;
                    yy = pathY1;
                }
                if (pathX2 > pathX1)
                    w = 1;
                for (int m = 0; m < dy + 1; m++)
                {
                    cells[yy + m, xx] = wallCell;
                    z = z - dx;
                    if (z <= 0)
                    {
                        xx = xx + w;
                        z = z + dy;
                    }
                }
            }

            // draw path (redraw grid)
            UniGrid.Children.RemoveRange(0, UniGrid.Children.Count);
            displaying();
        }

        private void optimGen(object sender, MouseButtonEventArgs e)
        {
            /*
                    static int[,,] paths = new int[gridSize, gridSize, 2];   // different paths, each item is x,y position of path
                    static int[,] pathLengths = new int[gridSize,2];         // length and how many ants used
                    static int pathses = 0;                                  // how many different paths got investigated

             */
            // clear data
            pathses = 0;
            for (int i = 0; i < gridSize; i++)
            {
                pathLengths[i, 0] = 0;
                pathLengths[i, 1] = 0;
                for (int j = 0; j < gridSize; j++)
                    paths[i, j] = false;
            }
            for (int j = 0; j < gridSize; j++)
                ts[j] = false;

            // find ant and food
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    if (cells[i, j] == foodCell)
                    {
                        foodX = j;
                        foodY = i;
                    }
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                    if (cells[i, j] == antCell)
                    {
                        antX = j;
                        antY = i;
                    }
            
            xFood = direction(antX, foodX);
            yFood = direction(antY, foodY);
            difX = (int)Math.Abs(foodX - antX);
            difY = (int)Math.Abs(foodY - antY);

            // main cycle for permutations of paths
            // explained here: https://betterexplained.com/articles/navigate-a-grid-using-combinations-and-permutations/
            // 

            // fill in all possible paths
            pathses = 0;
            generate2(difX, difY, ts);

            //  calculate number of walls in the path
            int minPath = 0;
            int whichPath = 0;
            for (int k = 0; k < pathses; k++)
            {
                int xx = antX;
                int yy = antY;
                int minCount = 0;
                for (int i = 0; i < difX; i++)
                {
                    if (paths[k, i])
                    {
                        xx = xx + xFood;
                        yy = yy + yFood;
                        if (cells[yy + 1, xx] == wallCell)
                            minCount++;
                    }
                    else
                    {
                        xx = xx + xFood;
                        if (cells[yy + 1, xx] == wallCell)
                            minCount++;
                    }
                }
                if (minCount > minPath)
                {
                    minPath = minCount;
                    whichPath = k;
                }
            }

            while (!foundFood)
            {


                System.Threading.Thread.Sleep(interval);
            }
            // use path

            /*
                            if (!foundFood & !sourced) // if have not setup source yet
                            {
                                sourceX = antX;
                                sourceY = antY;
                                sourced = true;
                                // calculate ideal path between ant and food
                            }
                            if (antX < 0) // there must be at least 1 ant to move, if not then generate from source (if any)
                            {
                                antX = sourceX;
                                antY = sourceY;
                                cells[antY, antX] = antCell;
                            }
                            if (!foundFood & (antX > -1))
                                {
                                    foundFood = false;
                                if (distance(antX, antY, foodX, foodY) < 2)
                                {
                                    foundFood = true;
                                    future[antY, antX] = antCell; // keep at the same place
                                    running = false;
                                    TimerStatus.Text = "Stopped";
                                }
                                else if ((cells[antY + 1, antX] == wallCell) | (cells[antY + 1, antX] == antCell)) // only evaluate move possibility if standing on wall or ant
                                {
                                    xFood = direction(antX, foodX);
                                    yFood = direction(antY, foodY);

                                    directCell = cells[antY + yFood, antX + xFood];
                                    undernCell = cells[antY + yFood + 1, antX + xFood];
                                    nextxCell = cells[antY, antX + xFood];
                                    underxCell = cells[antY + 1, antX + xFood];

                                    if (((directCell == emptyCell) & ((undernCell == wallCell) | (undernCell == antCell))) & (antX != foodX))
                                        future[antY + yFood, antX + xFood] = antCell;
                                    else if ((nextxCell == emptyCell) & ((underxCell == wallCell) | (underxCell == antCell)))
                                        future[antY, antX + xFood] = antCell;
                                    else if ((nextxCell == wallCell) | (nextxCell == antCell) & ((underxCell == wallCell) | (underxCell == antCell)))
                                    {
                                        future[antY, antX] = wallCell; // keep at the same place when having roadblock
                                        stoned = true;
                                    }
                                    else if (underxCell == emptyCell)  // fall down to empty place next to it
                                    {
                                        future[antY + 1, antX + xFood] = wallCell;
                                        stoned = true;
                                    }
                                    else
                                    {
                                        future[antY, antX] = wallCell; // keep at the same place
                                        stoned = true;
                                    }
                                }
                                else
                                    future[antY, antX] = antCell; // keep at the same place
                            }
                            else
                                future[antY, antX] = antCell; // keep at the same place

                        // wall itself stays - no action, just exists
                        for (int i = 0; i < gridSize; i++)
                            for (int j = 0; j < gridSize; j++)
                            {
                                if ((cells[i, j] == wallCell) & (future[i, j] == emptyCell))
                                {
                                    future[i, j] = wallCell;
                                    c++;
                                }
                                if ((cells[i, j] == foodCell) & (future[i, j] == emptyCell))
                                {
                                    future[i, j] = foodCell;
                                    c++;
                                }
                            }

                noCells.Text = c.ToString();
                if (c == 0)
                {
                    running = false;
                    TimerStatus.Text = "Stopped";
                }
                genCount++;
                TimerValue.Text = genCount.ToString();

                UniGrid.Children.RemoveRange(0, UniGrid.Children.Count);

                for (int i = 0; i < UniGrid.Columns * UniGrid.Rows; i++)
                    UniGrid.Children.Add(new Ellipse { Fill = new SolidColorBrush(cellsColors[future[i / gridSize, i % gridSize]]), Margin = new Thickness(1) });

                for (int i = 0; i < gridSize; i++)
                    for (int j = 0; j < gridSize; j++)
                    {
                        cells[i, j] = future[i, j];
                        future[i, j] = emptyCell;
                    }

             */

        }

        private void generate2(int n, int m, bool[] c)
        {
            if (m > 0) // diagonal move permutation
            {
                c[difX - n] = true;
                generate2(n - 1, m - 1, c);
                if (n == m)
                    return;
            }
            if (n > m) // straight move permutation
            {
                c[difX - n] = false;
                generate2(n - 1, m, c);
                return;
            }
            // store path
            for (int i = 0; i < difX; i++)
                paths[pathses, i] = c[i];
            pathses++;
        }

        private void clearGen(object sender, MouseButtonEventArgs e)
        {
            clearing();
            antGame = false;
            seekerGame = false;
            displaying();
        }

        private void randGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            if (Int32.TryParse(RandomCells.Text, out randomMax))
            {
                if ((randomMax < 1) | (randomMax > 99))
                    randomMax = 50;
            }
            else
                randomMax = 50;

            RandomCells.Text = randomMax.ToString();
            int c = 0;
            for (int i = 0; i < gridSize; i++)
                for (int j = 0; j < gridSize; j++)
                {
                    if (rand.Next(0,10000 / randomMax) <= 100)
                    {
                        cells[i, j] = normalCell;
                        c++;
                    }
                    UniGrid.Children.Add(new Ellipse { Fill = new SolidColorBrush(cellsColors[cells[i, j]]), Margin = new Thickness(1) });
            }
            noCells.Text = c.ToString();
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void gliderGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            cells[10, 10] = normalCell;
            cells[10, 11] = normalCell;
            cells[10, 12] = normalCell;
            cells[9, 12] = normalCell;
            cells[8, 11] = normalCell;

            displaying();

            noCells.Text = "5";
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void rocketGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            cells[10, 10] = normalCell;
            cells[10, 11] = normalCell;
            cells[9, 11] = normalCell;
            cells[9, 12] = normalCell;
            cells[11, 11] = normalCell;
            cells[11, 12] = normalCell;

            displaying();

            noCells.Text = "6";
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void huntingGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            cells[10, 10] = hunterCell;
            cells[10, 11] = foodCell;
            cells[10, 12] = foodCell;
            cells[9, 15] = foodCell;
            cells[7, 17] = foodCell;
            cells[10, 19] = foodCell;
            cells[13, 21] = foodCell;
            cells[16, 18] = foodCell;
            cells[19, 15] = foodCell;
            cells[19, 12] = foodCell;
            cells[19, 8] = foodCell;

            displaying();

            noCells.Text = "11";
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void staticGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            cells[10, 10] = normalCell;
            cells[10, 11] = normalCell;
            cells[9, 11] = normalCell;
            cells[9, 10] = normalCell;

            cells[15, 20] = normalCell;
            cells[14, 21] = normalCell;
            cells[14, 22] = normalCell;
            cells[15, 23] = normalCell;
            cells[16, 21] = normalCell;
            cells[16, 22] = normalCell;

            cells[20, 15] = normalCell;
            cells[19, 16] = normalCell;
            cells[21, 16] = normalCell;
            cells[19, 17] = normalCell;
            cells[22, 17] = normalCell;
            cells[20, 18] = normalCell;
            cells[21, 18] = normalCell;

            displaying();

            noCells.Text = "17";
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

        private void antsGen(object sender, MouseButtonEventArgs e)
        {
            clearing();

            cells[20, 5] = antCell;
            cells[21, 5] = wallCell;
            cells[21, 6] = wallCell;
            cells[21, 7] = wallCell;
            cells[14, 25] = foodCell;

            displaying();

            noCells.Text = "5";
            genCount = 0;
            TimerValue.Text = genCount.ToString();
        }

    }
}
